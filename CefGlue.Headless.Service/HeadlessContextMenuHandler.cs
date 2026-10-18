using System;
using Xilium.CefGlue.Common.Handlers;

namespace Xilium.CefGlue.Headless.Service
{
    internal sealed class HeadlessContextMenuHandler : ContextMenuHandler
    {
        public const int OpenLinkInNewTabCommand = (int)CefMenuId.UserFirst + 1;

        private readonly Action<string> _openInNewTab;

        public HeadlessContextMenuHandler(Action<string> openInNewTab)
        {
            _openInNewTab = openInNewTab;
        }

        protected override void OnBeforeContextMenu(CefBrowser browser, CefFrame frame, CefContextMenuParams state, CefMenuModel model)
        {
            if (IsOpenableLink(state.LinkUrl))
            {
                model.InsertItemAt(0, OpenLinkInNewTabCommand, "Open link in new tab");
                model.InsertSeparatorAt(1);
            }
        }

        protected override bool OnContextMenuCommand(CefBrowser browser, CefFrame frame, CefContextMenuParams state, int commandId, CefEventFlags eventFlags)
        {
            if (commandId != OpenLinkInNewTabCommand || !IsOpenableLink(state.LinkUrl))
            {
                return false;
            }

            _openInNewTab(state.LinkUrl);
            return true;
        }

        private static bool IsOpenableLink(string url) =>
            !string.IsNullOrEmpty(url)
            && (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
    }
}
