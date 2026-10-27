using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xilium.CefGlue.Broker.Storage;

namespace Xilium.CefGlue.Headless.Service
{
    internal sealed class LiveSettingsWatcher : IDisposable
    {
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);

        private readonly string _dbPath;
        private readonly int[] _ownPorts;
        private readonly Action _onApplied;
        private readonly Thread _thread;
        private readonly CancellationTokenSource _cts = new();
        private string _lastRevision;
        private bool _errorLogged;

        public LiveSettingsWatcher(string dbPath, string startRevision, int[] ownPorts, Action onApplied)
        {
            _dbPath = dbPath;
            _lastRevision = startRevision;
            _ownPorts = ownPorts;
            _onApplied = onApplied;
            _thread = new Thread(Run) { IsBackground = true, Name = "LiveSettingsWatcher" };
        }

        public void Start() => _thread.Start();

        public void Dispose() => _cts.Cancel();

        private void Run()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    if (_cts.Token.WaitHandle.WaitOne(Interval))
                    {
                        return;
                    }

                    PollOnce();
                    _errorLogged = false;
                }
                catch (Exception ex)
                {
                    if (!_errorLogged)
                    {
                        _errorLogged = true;
                        Console.WriteLine($"[Settings] Could not read live settings from broker.db - keeping the current ones, will keep trying: {ex.Message}");
                    }
                }
            }
        }

        public void PollOnce()
        {
            if (!System.IO.File.Exists(_dbPath))
            {
                return;
            }

            Dictionary<string, string> saved;
            using (var db = BrokerDb.OpenReadOnly(_dbPath))
            {
                saved = BrokerDb.ReadJson(db, BrokerDb.Settings);
            }

            var revision = BrokerDb.AsString(saved.GetValueOrDefault(BrokerSettingsWriter.SettingsRevisionKey));
            if (revision == _lastRevision)
            {
                return;
            }

            foreach (var (key, envName) in BrokerSettingsWriter.LiveKeys)
            {
                var value = BrokerDb.AsString(saved.GetValueOrDefault(key));
                Environment.SetEnvironmentVariable(envName, string.IsNullOrEmpty(value) ? null : value);
            }

            AddressPolicy.ConfigureFromEnvironment();
            AddressPolicy.AddReservedPorts(_ownPorts);
            _lastRevision = revision;

            Console.WriteLine($"[Settings] Revision {revision ?? "(none)"} applied live: " +
                $"recording {(RecordingSettings.Enabled ? "ON" : "off")} (max {RecordingSettings.MaxConcurrent} at once, {RecordingSettings.MaxMinutes} min), " +
                $"browsing filter mode {AddressPolicy.Mode}, enforcement {AddressPolicy.Enforcement}.");

            _onApplied?.Invoke();
        }
    }
}
