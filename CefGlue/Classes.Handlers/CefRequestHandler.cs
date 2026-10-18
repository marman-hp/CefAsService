namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefRequestHandler
    {
        private int on_before_browse(cef_request_handler_t* self, cef_browser_t* browser, cef_frame_t* frame, cef_request_t* request, int user_gesture, int is_redirect)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var m_frame = CefFrame.FromNative(frame);
            var m_request = CefRequest.FromNative(request);
            var m_userGesture = user_gesture != 0;
            var m_isRedirect = is_redirect != 0;

            var result = OnBeforeBrowse(m_browser, m_frame, m_request, m_userGesture, m_isRedirect);

            return result ? 1 : 0;
        }

        protected virtual bool OnBeforeBrowse(CefBrowser browser, CefFrame frame, CefRequest request, bool userGesture, bool isRedirect)
        {
            return false;
        }

        private int on_open_urlfrom_tab(cef_request_handler_t* self, cef_browser_t* browser, cef_frame_t* frame, cef_string_t* target_url, CefWindowOpenDisposition target_disposition, int user_gesture)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var m_frame = CefFrame.FromNative(frame);
            var m_targetUrl = cef_string_t.ToString(target_url);
            var m_userGesture = user_gesture != 0;

            var m_result = OnOpenUrlFromTab(m_browser, m_frame, m_targetUrl, target_disposition, m_userGesture);

            return m_result ? 1 : 0;
        }

        protected virtual bool OnOpenUrlFromTab(CefBrowser browser, CefFrame frame, string targetUrl, CefWindowOpenDisposition targetDisposition, bool userGesture)
        {
            return false;
        }

        private cef_resource_request_handler_t* get_resource_request_handler(cef_request_handler_t* self, cef_browser_t* browser, cef_frame_t* frame, cef_request_t* request, int is_navigation, int is_download, cef_string_t* request_initiator, int* disable_default_handling)
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

        private int get_auth_credentials(cef_request_handler_t* self, cef_browser_t* browser, cef_string_t* origin_url, int isProxy, cef_string_t* host, int port, cef_string_t* realm, cef_string_t* scheme, cef_auth_callback_t* callback)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var m_originUrl = cef_string_t.ToString(origin_url);
            var m_host = cef_string_t.ToString(host);
            var m_realm = cef_string_t.ToString(realm);
            var m_scheme = cef_string_t.ToString(scheme);
            var m_callback = CefAuthCallback.FromNative(callback);

            var result = GetAuthCredentials(m_browser, m_originUrl, isProxy != 0, m_host, port, m_realm, m_scheme, m_callback);

            return result ? 1 : 0;
        }

        protected virtual bool GetAuthCredentials(CefBrowser browser, string originUrl, bool isProxy, string host, int port, string realm, string scheme, CefAuthCallback callback)
        {
            return false;
        }

        private int on_certificate_error(cef_request_handler_t* self, cef_browser_t* browser, CefErrorCode cert_error, cef_string_t* request_url, cef_sslinfo_t* ssl_info, cef_callback_t* callback)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var m_request_url = cef_string_t.ToString(request_url);
            var m_ssl_info = CefSslInfo.FromNative(ssl_info);
            var m_callback = CefCallback.FromNativeOrNull(callback);

            var result = OnCertificateError(m_browser, cert_error, m_request_url, m_ssl_info, m_callback);

            return result ? 1 : 0;
        }

        protected virtual bool OnCertificateError(CefBrowser browser, CefErrorCode certError, string requestUrl, CefSslInfo sslInfo, CefCallback callback)
        {
            return false;
        }

        private int on_select_client_certificate(cef_request_handler_t* self, cef_browser_t* browser, int isProxy, cef_string_t* host, int port, UIntPtr certificatesCount, cef_x509_certificate_t** certificates, cef_select_client_certificate_callback_t* callback)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var m_isProxy = isProxy != 0;
            var m_host = cef_string_t.ToString(host);
            var m_certCount = checked((int)certificatesCount);
            var m_certificates = new CefX509Certificate[m_certCount];
            for (var i = 0; i < m_certCount; i++)
            {
                m_certificates[i] = CefX509Certificate.FromNative(certificates[i]);
            }
            var m_callback = CefSelectClientCertificateCallback.FromNative(callback);

            var result = OnSelectClientCertificate(m_browser, m_isProxy, m_host, port, m_certificates, m_callback);
            if (result)
            {
                return 1;
            }
            else
            {
                m_callback.Dispose();
                return 0;
            }
        }

        protected virtual bool OnSelectClientCertificate(CefBrowser browser, bool isProxy, string host, int port, CefX509Certificate[] certificates, CefSelectClientCertificateCallback callback)
        {
            return false;
        }

        private void on_render_view_ready(cef_request_handler_t* self, cef_browser_t* browser)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            OnRenderViewReady(m_browser);
        }

        protected virtual void OnRenderViewReady(CefBrowser browser)
        {
        }

        private void on_render_process_terminated(cef_request_handler_t* self, cef_browser_t* browser, CefTerminationStatus status, int error_code, cef_string_t* error_string)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var error = cef_string_t.ToString(error_string);

            OnRenderProcessTerminated(m_browser, status, error_code, error);
        }

        protected virtual void OnRenderProcessTerminated(CefBrowser browser, CefTerminationStatus status, int error_code, string error)
        {
        }

        private int on_render_process_unresponsive(cef_request_handler_t* self, cef_browser_t* browser, cef_unresponsive_process_callback_t* callback)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var m_callback = CefUnresponsiveProcessCallback.FromNative(callback);

            return OnRenderProcessUnresponsive(m_browser, m_callback) ? 1 : 0;
        }

        protected virtual bool OnRenderProcessUnresponsive(CefBrowser browser, CefUnresponsiveProcessCallback callback)
        {
            return false;
        }

        private void on_render_process_responsive(cef_request_handler_t* self, cef_browser_t* browser)
        {
            CheckSelf(self);
            OnRenderProcessResponsive(CefBrowser.FromNative(browser));
        }

        protected virtual void OnRenderProcessResponsive(CefBrowser browser)
        {
        }

        private void on_document_available_in_main_frame(cef_request_handler_t* self, cef_browser_t* browser)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);

            OnDocumentAvailableInMainFrame(m_browser);
        }

        protected virtual void OnDocumentAvailableInMainFrame(CefBrowser browser)
        {
        }
    }
}
