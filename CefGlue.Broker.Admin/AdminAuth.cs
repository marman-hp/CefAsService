using System;
using System.Security.Cryptography;
using System.Text;

namespace Xilium.CefGlue.Broker.Admin
{
    internal sealed class AdminAuth
    {
        public const int MinPasswordLength = 8;
        private const int Iterations = 210_000;
        private const int SaltBytes = 16;
        private const int HashBytes = 32;
        private const int MaxFailures = 5;

        private static readonly TimeSpan[] LockoutSteps = { TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30) };

        private readonly AdminSettingsStore _settings;
        private readonly object _gate = new();
        private int _consecutiveFailures;
        private int _lockoutLevel;
        private DateTime _lockedUntilUtc;
        private bool _refusedWhileLockedLogged;
        private int _failuresSinceSignIn;
        private DateTime _lastFailureUtc;
        private (int Count, DateTime LastFailureUtc)? _pendingNotice;

        public AdminAuth(AdminSettingsStore settings)
        {
            _settings = settings;
        }

        public bool HasPassword => !string.IsNullOrEmpty(_settings.Snapshot().PasswordHash);

        public string PasswordStamp
        {
            get
            {
                var hash = _settings.Snapshot().PasswordHash;
                return string.IsNullOrEmpty(hash)
                    ? null
                    : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hash)))[..16];
            }
        }

        public TimeSpan? LockoutRemaining
        {
            get
            {
                lock (_gate)
                {
                    var remaining = _lockedUntilUtc - DateTime.UtcNow;
                    return remaining > TimeSpan.Zero ? remaining : null;
                }
            }
        }

        public static string ValidateNewPassword(string password, string confirm)
        {
            if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            {
                return $"Password must be at least {MinPasswordLength} characters.";
            }

            return password == confirm ? null : "The two passwords don't match.";
        }

        public void SetPassword(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltBytes);
            var hash = Derive(password, salt, Iterations);
            _settings.Update(s =>
            {
                s.PasswordHash = Convert.ToBase64String(hash);
                s.PasswordSalt = Convert.ToBase64String(salt);
                s.PasswordIterations = Iterations;
            });
        }

        public bool TryLogin(string password)
        {
            var ok = Verify(password);
            lock (_gate)
            {
                if (ok)
                {
                    if (_failuresSinceSignIn > 0)
                    {
                        _pendingNotice = (_failuresSinceSignIn, _lastFailureUtc);
                    }

                    _consecutiveFailures = 0;
                    _lockoutLevel = 0;
                    _failuresSinceSignIn = 0;
                    _refusedWhileLockedLogged = false;
                }
                else
                {
                    _failuresSinceSignIn++;
                    _lastFailureUtc = DateTime.UtcNow;
                    _consecutiveFailures++;
                    Console.WriteLine($"[Admin] Failed login attempt - {_consecutiveFailures} in a row, {_failuresSinceSignIn} since the last successful sign-in.");

                    if (_consecutiveFailures >= MaxFailures)
                    {
                        var duration = LockoutSteps[Math.Min(_lockoutLevel, LockoutSteps.Length - 1)];
                        _lockoutLevel++;
                        _consecutiveFailures = 0;
                        _lockedUntilUtc = DateTime.UtcNow + duration;
                        _refusedWhileLockedLogged = false;
                        Console.WriteLine($"[Admin] {MaxFailures} failed login attempts in a row - login locked for {FormatDuration(duration)} (lock #{_lockoutLevel} since the last successful sign-in).");
                    }
                }
            }

            return ok;
        }

        public void NoteRefusedWhileLocked()
        {
            lock (_gate)
            {
                if (_refusedWhileLockedLogged)
                {
                    return;
                }

                _refusedWhileLockedLogged = true;
                Console.WriteLine("[Admin] Login attempt refused: the login is locked after repeated failures.");
            }
        }

        public (int Count, DateTime LastFailureUtc)? PendingLoginNotice
        {
            get
            {
                lock (_gate)
                {
                    return _pendingNotice;
                }
            }
        }

        public void AcknowledgeLoginNotice()
        {
            lock (_gate)
            {
                _pendingNotice = null;
            }
        }

        public static string FormatDuration(TimeSpan span)
        {
            if (span.TotalSeconds < 90)
            {
                return $"{Math.Ceiling(span.TotalSeconds):0} seconds";
            }

            var minutes = (int)Math.Ceiling(span.TotalMinutes);
            return minutes < 60 ? $"{minutes} minutes" : $"{minutes / 60} hour{(minutes / 60 == 1 ? "" : "s")}{(minutes % 60 == 0 ? "" : $" {minutes % 60} minutes")}";
        }

        public bool Verify(string password)
        {
            var data = _settings.Snapshot();
            if (string.IsNullOrEmpty(data.PasswordHash) || password == null)
            {
                return false;
            }

            var expected = Convert.FromBase64String(data.PasswordHash);
            var actual = Derive(password, Convert.FromBase64String(data.PasswordSalt), data.PasswordIterations);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }

        private static byte[] Derive(string password, byte[] salt, int iterations) =>
            Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, HashBytes);
    }
}
