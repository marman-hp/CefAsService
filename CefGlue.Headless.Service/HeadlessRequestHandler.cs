using System;
using Xilium.CefGlue.Common.Handlers;

namespace Xilium.CefGlue.Headless.Service
{
    internal sealed class HeadlessRequestHandler : RequestHandler
    {
        private readonly BrowserSession _session;

        private readonly FilterResourceRequestHandler _resourceHandler;

        public HeadlessRequestHandler(BrowserSession session)
        {
            _session = session;
            _resourceHandler = new FilterResourceRequestHandler(session);
        }

        protected override bool OnBeforeBrowse(CefBrowser browser, CefFrame frame, CefRequest request, bool userGesture, bool isRedirect)
        {
            BrowsingFilterLog.Observe(request.Url, isRedirect ? "redirect" : "navigation", _session.Id);
            return false;
        }

        protected override CefResourceRequestHandler GetResourceRequestHandler(CefBrowser browser, CefFrame frame, CefRequest request, bool isNavigation, bool isDownload, string requestInitiator, ref bool disableDefaultHandling)
        {
            return _resourceHandler;
        }

        protected override bool GetAuthCredentials(CefBrowser browser, string originUrl, bool isProxy, string host, int port, string realm, string scheme, CefAuthCallback callback)
        {
            return isProxy && WorkerProxy.TryAnswerAuth(_session.Id, host, port, callback);
        }

        protected override void OnRenderProcessTerminated(CefBrowser browser, CefTerminationStatus status, int errorCode, string errorString)
        {
            ErrorLog.Append(ErrorLog.ServiceFileName, AppContext.BaseDirectory,
                $"Renderer process terminated - status {status}, error code 0x{errorCode:X8}{(string.IsNullOrEmpty(errorString) ? "" : $" ({errorString})")}, " +
                $"worker pid {Environment.ProcessId}, tenant '{Program.TenantId ?? "(standalone)"}', tab '{_session.Id}', url '{_session.CurrentUrl}', see {Program.CefLogFileName}");
        }
    }
}
