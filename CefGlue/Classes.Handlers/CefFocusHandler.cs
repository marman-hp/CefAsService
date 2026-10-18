namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefFocusHandler
    {
        private void on_take_focus(cef_focus_handler_t* self, cef_browser_t* browser, int next)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);

            OnTakeFocus(m_browser, next != 0);
        }

        protected virtual void OnTakeFocus(CefBrowser browser, bool next)
        {
        }

        private int on_set_focus(cef_focus_handler_t* self, cef_browser_t* browser, CefFocusSource source)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);

            return OnSetFocus(m_browser, source) ? 1 : 0;
        }

        protected virtual bool OnSetFocus(CefBrowser browser, CefFocusSource source)
        {
            return false;
        }

        private void on_got_focus(cef_focus_handler_t* self, cef_browser_t* browser)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);

            OnGotFocus(m_browser);
        }

        protected virtual void OnGotFocus(CefBrowser browser)
        {
        }
    }
}
