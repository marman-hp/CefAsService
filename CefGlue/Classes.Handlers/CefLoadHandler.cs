namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefLoadHandler
    {
        private void on_loading_state_change(cef_load_handler_t* self, cef_browser_t* browser, int isLoading, int canGoBack, int canGoForward)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);

            OnLoadingStateChange(mBrowser, isLoading != 0, canGoBack != 0, canGoForward != 0);
        }

        protected virtual void OnLoadingStateChange(CefBrowser browser, bool isLoading, bool canGoBack, bool canGoForward)
        {
        }

        private void on_load_start(cef_load_handler_t* self, cef_browser_t* browser, cef_frame_t* frame, CefTransitionType transition_type)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var m_frame = CefFrame.FromNative(frame);

            OnLoadStart(m_browser, m_frame, transition_type);
        }

        protected virtual void OnLoadStart(CefBrowser browser, CefFrame frame, CefTransitionType transitionType)
        {
        }

        private void on_load_end(cef_load_handler_t* self, cef_browser_t* browser, cef_frame_t* frame, int httpStatusCode)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var m_frame = CefFrame.FromNative(frame);

            OnLoadEnd(m_browser, m_frame, httpStatusCode);
        }

        protected virtual void OnLoadEnd(CefBrowser browser, CefFrame frame, int httpStatusCode)
        {
        }

        private void on_load_error(cef_load_handler_t* self, cef_browser_t* browser, cef_frame_t* frame, CefErrorCode errorCode, cef_string_t* errorText, cef_string_t* failedUrl)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var m_frame = CefFrame.FromNative(frame);
            var m_errorText = cef_string_t.ToString(errorText);
            var m_failedUrl = cef_string_t.ToString(failedUrl);

            OnLoadError(m_browser, m_frame, errorCode, m_errorText, m_failedUrl);
        }

        protected virtual void OnLoadError(CefBrowser browser, CefFrame frame, CefErrorCode errorCode, string errorText, string failedUrl)
        {
        }
    }
}
