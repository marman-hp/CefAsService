namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefSchemeHandlerFactory
    {
        private cef_resource_handler_t* create(cef_scheme_handler_factory_t* self, cef_browser_t* browser, cef_frame_t* frame, cef_string_t* scheme_name, cef_request_t* request)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNativeOrNull(browser);
            var m_frame = CefFrame.FromNativeOrNull(frame);
            var m_schemeName = cef_string_t.ToString(scheme_name);
            var m_request = CefRequest.FromNative(request);

            var handler = Create(m_browser, m_frame, m_schemeName, m_request);

            return handler != null ? handler.ToNative() : null;
        }

        protected abstract CefResourceHandler Create(CefBrowser browser, CefFrame frame, string schemeName, CefRequest request);
    }
}
