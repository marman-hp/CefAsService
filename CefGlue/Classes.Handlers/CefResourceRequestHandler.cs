namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefResourceRequestHandler
    {
        private cef_cookie_access_filter_t* get_cookie_access_filter(cef_resource_request_handler_t* self, cef_browser_t* browser, cef_frame_t* frame, cef_request_t* request)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNativeOrNull(browser);
            var m_frame = CefFrame.FromNativeOrNull(frame);
            var m_request = CefRequest.FromNative(request);

            var m_result = GetCookieAccessFilter(m_browser, m_frame, m_request);

            return m_result != null ? m_result.ToNative() : null;
        }

        protected abstract CefCookieAccessFilter GetCookieAccessFilter(CefBrowser browser, CefFrame frame, CefRequest request);

        private CefReturnValue on_before_resource_load(cef_resource_request_handler_t* self, cef_browser_t* browser, cef_frame_t* frame, cef_request_t* request, cef_callback_t* callback)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNativeOrNull(browser);
            var m_frame = CefFrame.FromNativeOrNull(frame);
            var m_request = CefRequest.FromNative(request);
            var m_callback = CefCallback.FromNative(callback);

            var result = OnBeforeResourceLoad(m_browser, m_frame, m_request, m_callback);

            if (result != CefReturnValue.ContinueAsync)
            {
                m_browser.Dispose();
                m_frame.Dispose();
                m_request.Dispose();
                m_callback.Dispose();
            }

            return result;
        }

        protected virtual CefReturnValue OnBeforeResourceLoad(CefBrowser browser, CefFrame frame, CefRequest request, CefCallback callback)
        {
            return CefReturnValue.Continue;
        }

        private cef_resource_handler_t* get_resource_handler(cef_resource_request_handler_t* self, cef_browser_t* browser, cef_frame_t* frame, cef_request_t* request)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNativeOrNull(browser);
            var m_frame = CefFrame.FromNativeOrNull(frame);
            using (var m_request = CefRequest.FromNative(request))
            {
                var handler = GetResourceHandler(m_browser, m_frame, m_request);
                return handler != null ? handler.ToNative() : null;
            }
        }

        protected virtual CefResourceHandler? GetResourceHandler(CefBrowser browser, CefFrame frame, CefRequest request)
            => null;

        private void on_resource_redirect(cef_resource_request_handler_t* self, cef_browser_t* browser, cef_frame_t* frame, cef_request_t* request, cef_response_t* response, cef_string_t* new_url)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNativeOrNull(browser);
            var m_frame = CefFrame.FromNativeOrNull(frame);
            var m_request = CefRequest.FromNative(request);
            var m_response = CefResponse.FromNative(response);
            var m_newUrl = cef_string_t.ToString(new_url);

            var o_newUrl = m_newUrl;
            OnResourceRedirect(m_browser, m_frame, m_request, m_response, ref m_newUrl);

            if ((object)m_newUrl != (object)o_newUrl)
            {
                cef_string_t.Copy(m_newUrl, new_url);
            }
        }

        protected virtual void OnResourceRedirect(CefBrowser browser, CefFrame frame, CefRequest request, CefResponse response, ref string newUrl)
        {
        }

        private int on_resource_response(cef_resource_request_handler_t* self, cef_browser_t* browser, cef_frame_t* frame, cef_request_t* request, cef_response_t* response)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNativeOrNull(browser);
            var m_frame = CefFrame.FromNativeOrNull(frame);
            var m_request = CefRequest.FromNative(request);
            var m_response = CefResponse.FromNative(response);

            var m_result = OnResourceResponse(m_browser, m_frame, m_request, m_response);

            return m_result ? 1 : 0;
        }

        protected virtual bool OnResourceResponse(CefBrowser browser, CefFrame frame, CefRequest request, CefResponse response)
        {
            return false;
        }

        private cef_response_filter_t* get_resource_response_filter(cef_resource_request_handler_t* self, cef_browser_t* browser, cef_frame_t* frame, cef_request_t* request, cef_response_t* response)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNativeOrNull(browser);
            var mFrame = CefFrame.FromNativeOrNull(frame);
            var mRequest = CefRequest.FromNative(request);
            var mResponse = CefResponse.FromNative(response);

            var result = GetResourceResponseFilter(mBrowser, mFrame, mRequest, mResponse);

            if (result != null)
            {
                return result.ToNative();
            }

            return null;
        }

        protected virtual CefResponseFilter? GetResourceResponseFilter(CefBrowser browser, CefFrame frame, CefRequest request, CefResponse response)
            => null;

        private void on_resource_load_complete(cef_resource_request_handler_t* self, cef_browser_t* browser, cef_frame_t* frame, cef_request_t* request, cef_response_t* response, CefUrlRequestStatus status, long received_content_length)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNativeOrNull(browser);
            var mFrame = CefFrame.FromNativeOrNull(frame);
            var mRequest = CefRequest.FromNative(request);
            var mResponse = CefResponse.FromNative(response);

            OnResourceLoadComplete(mBrowser, mFrame, mRequest, mResponse, status, received_content_length);
        }

        protected virtual void OnResourceLoadComplete(CefBrowser browser, CefFrame frame, CefRequest request, CefResponse response, CefUrlRequestStatus status, long receivedContentLength)
        {
        }

        private void on_protocol_execution(cef_resource_request_handler_t* self, cef_browser_t* browser, cef_frame_t* frame, cef_request_t* request, int* allow_os_execution)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNativeOrNull(browser);
            var m_frame = CefFrame.FromNativeOrNull(frame);
            var m_request = CefRequest.FromNative(request);
            var m_allow_os_execution = *allow_os_execution != 0;

            OnProtocolExecution(m_browser, m_frame, m_request, ref m_allow_os_execution);

            *allow_os_execution = m_allow_os_execution ? 1 : 0;
        }

        protected virtual void OnProtocolExecution(CefBrowser browser, CefFrame frame, CefRequest request, ref bool allowOSExecution)
        {
        }
    }
}
