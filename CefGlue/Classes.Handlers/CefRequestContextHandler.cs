namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefRequestContextHandler
    {
        private void on_request_context_initialized(cef_request_context_handler_t* self, cef_request_context_t* request_context)
        {
            CheckSelf(self);

            var mRequestContext = CefRequestContext.FromNative(request_context);
            OnRequestContextInitialized(mRequestContext);
        }

        protected virtual void OnRequestContextInitialized(CefRequestContext requestContext) { }

        private cef_resource_request_handler_t* get_resource_request_handler(cef_request_context_handler_t* self, cef_browser_t* browser, cef_frame_t* frame, cef_request_t* request, int is_navigation, int is_download, cef_string_t* request_initiator, int* disable_default_handling)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNativeOrNull(browser);
            var m_frame = CefFrame.FromNativeOrNull(frame);
            var m_request = CefRequest.FromNative(request);
            var m_isNavigation = is_navigation != 0;
            var m_isDownload = is_download != 0;
            var m_requestInitiator = cef_string_t.ToString(request_initiator);
            var m_disableDefaultHandling = *disable_default_handling != 0;

            var m_result = GetResourceRequestHandler(m_browser, m_frame, m_request, m_isNavigation, m_isDownload, m_requestInitiator, ref m_disableDefaultHandling);

            *disable_default_handling = m_disableDefaultHandling ? 1 : 0;

            return m_result != null ? m_result.ToNative() : null;
        }

        protected abstract CefResourceRequestHandler GetResourceRequestHandler(CefBrowser browser, CefFrame frame, CefRequest request, bool isNavigation, bool isDownload, string requestInitiator, ref bool disableDefaultHandling);
    }
}
