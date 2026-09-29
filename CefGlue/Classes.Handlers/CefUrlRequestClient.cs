namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;
    using System.IO;

    public abstract unsafe partial class CefUrlRequestClient
    {
        private void on_request_complete(cef_urlrequest_client_t* self, cef_urlrequest_t* request)
        {
            CheckSelf(self);

            var m_request = CefUrlRequest.FromNative(request);

            OnRequestComplete(m_request);
        }

        protected abstract void OnRequestComplete(CefUrlRequest request);

        private void on_upload_progress(cef_urlrequest_client_t* self, cef_urlrequest_t* request, long current, long total)
        {
            CheckSelf(self);

            var m_request = CefUrlRequest.FromNative(request);

            OnUploadProgress(m_request, current, total);
        }

        protected abstract void OnUploadProgress(CefUrlRequest request, long current, long total);

        private void on_download_progress(cef_urlrequest_client_t* self, cef_urlrequest_t* request, long current, long total)
        {
            CheckSelf(self);

            var m_request = CefUrlRequest.FromNative(request);

            OnDownloadProgress(m_request, current, total);
        }

        protected abstract void OnDownloadProgress(CefUrlRequest request, long current, long total);

        private void on_download_data(cef_urlrequest_client_t* self, cef_urlrequest_t* request, void* data, UIntPtr data_length)
        {
            CheckSelf(self);

            var m_request = CefUrlRequest.FromNative(request);

            using (var stream = new UnmanagedMemoryStream((byte*)data, (long)data_length))
            {
                OnDownloadData(m_request, stream);
            }
        }

        protected abstract void OnDownloadData(CefUrlRequest request, Stream data);

        private int get_auth_credentials(cef_urlrequest_client_t* self, int isProxy, cef_string_t* host, int port, cef_string_t* realm, cef_string_t* scheme, cef_auth_callback_t* callback)
        {
            CheckSelf(self);

            var m_isProxy = isProxy != 0;
            var m_host = cef_string_t.ToString(host);
            var m_realm = cef_string_t.ToString(realm);
            var m_scheme = cef_string_t.ToString(scheme);
            var m_callback = CefAuthCallback.FromNative(callback);

            var m_result = GetAuthCredentials(m_isProxy, m_host, port, m_realm, m_scheme, m_callback);

            return m_result ? 1 : 0;
        }

        protected virtual bool GetAuthCredentials(bool isProxy, string host, int port, string realm, string scheme, CefAuthCallback callback)
            => false;
    }
}
