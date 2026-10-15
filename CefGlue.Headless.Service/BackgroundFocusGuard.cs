using System;
using Xilium.CefGlue.Common.Handlers;

namespace Xilium.CefGlue.Headless.Service
{
    internal sealed class BackgroundFocusGuard : FocusHandler
    {
        private readonly string _sessionId;
        private readonly Func<string> _selectedId;

        public BackgroundFocusGuard(string sessionId, Func<string> selectedId)
        {
            _sessionId = sessionId;
            _selectedId = selectedId;
        }

        protected override bool OnSetFocus(CefBrowser browser, CefFocusSource source)
            => _selectedId() != _sessionId;
    }
}
