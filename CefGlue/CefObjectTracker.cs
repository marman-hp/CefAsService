using System;
using System.Collections.Generic;
using System.Linq;

namespace Xilium.CefGlue
{
    public static class CefObjectTracker
    {
        [ThreadStatic]
        private static HashSet<IDisposable> _disposables;

        private class TrackingSession : IDisposable
        {
            public static readonly TrackingSession Default = new TrackingSession(stopTracking: true);
            public static readonly TrackingSession Nop = new TrackingSession(stopTracking: false);

            private readonly bool _stopTracking;

            private TrackingSession(bool stopTracking)
            {
                _stopTracking = stopTracking;
            }

            public void Dispose()
            {
                if (_stopTracking)
                {
                    StopTracking();
                }
            }
        }

        public static IDisposable StartTracking()
        {
            if (_disposables != null)
            {
                return TrackingSession.Nop;
            }
            _disposables = new HashSet<IDisposable>();
            return TrackingSession.Default;
        }

        public static void StopTracking()
        {
            if (_disposables == null)
            {
                return;
            }

            var disposables = _disposables.ToArray();
            _disposables = null;

            foreach (var disposable in disposables)
            {
                disposable.Dispose();
            }
        }

        public static void Track(IDisposable obj)
        {
            if (_disposables != null)
            {
                _disposables.Add(obj);
            }
        }

        public static void Untrack(IDisposable obj)
        {
            if (_disposables != null)
            {
                _disposables.Remove(obj);
            }
        }
    }
}
