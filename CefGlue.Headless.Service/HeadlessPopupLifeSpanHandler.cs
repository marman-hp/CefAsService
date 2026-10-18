using System.Threading.Tasks;
using Xilium.CefGlue.Common.Handlers;
using Xilium.CefGlue.Headless;

namespace Xilium.CefGlue.Headless.Service
{
    internal sealed class HeadlessPopupLifeSpanHandler : LifeSpanHandler
    {
        private readonly SessionManager _sessionManager;

        public HeadlessPopupLifeSpanHandler(SessionManager sessionManager)
        {
            _sessionManager = sessionManager;
        }

        protected override bool OnBeforePopup(
            CefBrowser browser,
            CefFrame frame,
            int popupId,
            string targetUrl,
            string targetFrameName,
            CefWindowOpenDisposition targetDisposition,
            bool userGesture,
            CefPopupFeatures popupFeatures,
            CefWindowInfo windowInfo,
            ref CefClient client,
            CefBrowserSettings settings,
            ref CefDictionaryValue extraInfo,
            ref bool noJavascriptAccess)
        {
            var opensNewSession = targetDisposition == CefWindowOpenDisposition.NewPopup
                || targetDisposition == CefWindowOpenDisposition.NewForegroundTab
                || targetDisposition == CefWindowOpenDisposition.NewBackgroundTab
                || targetDisposition == CefWindowOpenDisposition.NewWindow;

            if (!opensNewSession)
            {
                frame.LoadUrl(targetUrl);
                return true;
            }

            var openerId = _sessionManager.FindSessionId(browser);

            var isNewPopup = targetDisposition == CefWindowOpenDisposition.NewPopup;
            int popupWidth, popupHeight;

            if (isNewPopup)
            {
                popupWidth = popupFeatures?.Width ?? 1280;
                popupHeight = popupFeatures?.Height ?? 800;
            }
            else if (openerId != null && _sessionManager.TryGetSession(openerId, out var openerSession))
            {
                popupWidth = openerSession.Browser.Target.Width;
                popupHeight = openerSession.Browser.Target.Height;
            }
            else
            {
                popupWidth = 1280;
                popupHeight = 800;
            }

            var popupBrowser = SessionManager.CreateHeadlessBrowser(forPopup: true, width: popupWidth, height: popupHeight);

            var bounds = windowInfo.Bounds;
            windowInfo.Bounds = new CefRectangle(bounds.X, bounds.Y, popupWidth, popupHeight);

            CefClient outClient = null;
            CefDictionaryValue outExtra = null;
            popupBrowser.OnBeforeCreatePopup(ref outClient, ref outExtra, windowInfo, popupFeatures);

            client = outClient;
            extraInfo = outExtra;

            var isRealPopup = targetDisposition == CefWindowOpenDisposition.NewPopup;
            _sessionManager.AdoptPopupSession(popupBrowser, targetUrl, openerId, isRealPopup);

            return false;
        }

        protected override bool DoClose(CefBrowser browser)
        {
            var id = _sessionManager.FindSessionId(browser);
            if (id != null && _sessionManager.TryGetSession(id, out var session) && session.IsPopup)
            {
                Task.Run(() => _sessionManager.RemoveBrowser(id));
                return true;
            }

            return false;
        }
    }
}
