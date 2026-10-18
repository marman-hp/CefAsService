using System;
using System.Threading;

namespace Xilium.CefGlue.Headless.Service
{
    internal sealed class AbandonmentWatchdog : IDisposable
    {
        private readonly TimeSpan _gracePeriod;
        private readonly Action _onAbandoned;
        private readonly object _lock = new object();
        private Timer _timer;

        public AbandonmentWatchdog(TimeSpan gracePeriod, Action onAbandoned)
        {
            _gracePeriod = gracePeriod;
            _onAbandoned = onAbandoned;
        }

        public void Start()
        {
            lock (_lock)
            {
                _timer?.Dispose();
                _timer = new Timer(_ => _onAbandoned(), null, _gracePeriod, Timeout.InfiniteTimeSpan);
            }
        }

        public void Cancel()
        {
            lock (_lock)
            {
                _timer?.Dispose();
                _timer = null;
            }
        }

        public void Dispose() => Cancel();
    }
}
