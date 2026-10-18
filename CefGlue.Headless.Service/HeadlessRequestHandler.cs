using System;
using Xilium.CefGlue.Common.Handlers;

namespace Xilium.CefGlue.Headless.Service
{
    internal sealed class HeadlessRequestHandler : RequestHandler
    {
        private readonly BrowserSession _session;

        public HeadlessRequestHandler(BrowserSession session)
        {
            _session = session;
        }

        protected override void OnRenderProcessTerminated(CefBrowser browser, CefTerminationStatus status, int errorCode, string errorString)
        {
            ErrorLog.Append(ErrorLog.ServiceFileName, AppContext.BaseDirectory,
                $"Renderer process terminated - status {status}, error code 0x{errorCode:X8}{(string.IsNullOrEmpty(errorString) ? "" : $" ({errorString})")}, " +
                $"worker pid {Environment.ProcessId}, tenant '{Program.TenantId ?? "(standalone)"}', tab '{_session.Id}', url '{_session.CurrentUrl}', see {Program.CefLogFileName}");
        }
    }
}
