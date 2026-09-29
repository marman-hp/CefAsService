namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefFindHandler
    {
        private void on_find_result(cef_find_handler_t* self, cef_browser_t* browser, int identifier, int count, cef_rect_t* selectionRect, int activeMatchOrdinal, int finalUpdate)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            var mSelectionRect = new CefRectangle(selectionRect->x, selectionRect->y, selectionRect->width, selectionRect->height);

            OnFindResult(mBrowser, identifier, count, mSelectionRect, activeMatchOrdinal, finalUpdate != 0);
        }

        protected abstract void OnFindResult(CefBrowser browser, int identifier, int count, CefRectangle selectionRect, int activeMatchOrdinal, bool finalUpdate);
    }
}
