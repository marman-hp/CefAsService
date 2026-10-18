namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;
    using System.Threading;
    using System.IO;

    public abstract unsafe partial class CefResourceHandler
    {
        private int _keepObject;

        public void KeepObject()
        {
            if (Interlocked.CompareExchange(ref _keepObject, 1, 0) == 0)
            {
                add_ref(_self);
            }
        }

        public void ReleaseObject()
        {
            if (Interlocked.CompareExchange(ref _keepObject, 0, 1) == 1)
            {
                release(_self);
            }
        }

        private int open(cef_resource_handler_t* self, cef_request_t* request, int* handle_request, cef_callback_t* callback)
        {
            CheckSelf(self);

            var m_request = CefRequest.FromNative(request);
            var m_callback = CefCallback.FromNative(callback);

            var m_result = Open(m_request, out var m_handleRequest, m_callback);

            *handle_request = m_handleRequest ? 1 : 0;

            return m_result ? 1 : 0;
        }

        protected abstract bool Open(CefRequest request, out bool handleRequest, CefCallback callback);

        private int process_request(cef_resource_handler_t* self, cef_request_t* request, cef_callback_t* callback)
        {
            CheckSelf(self);

            var m_request = CefRequest.FromNative(request);
            var m_callback = CefCallback.FromNative(callback);

#pragma warning disable CS0618
            var result = ProcessRequest(m_request, m_callback);
#pragma warning restore CS0618
            return result ? 1 : 0;
        }

        [Obsolete("This method is deprecated. Use Open instead.")]
        protected virtual bool ProcessRequest(CefRequest request, CefCallback callback)
        {
            request.Dispose();
            callback.Dispose();
            return false;
        }

        private void get_response_headers(cef_resource_handler_t* self, cef_response_t* response, long* response_length, cef_string_t* redirectUrl)
        {
            CheckSelf(self);

            var m_response = CefResponse.FromNative(response);
            long m_responseLength;
            string m_redirectUrl;

            GetResponseHeaders(m_response, out m_responseLength, out m_redirectUrl);

            *response_length = m_responseLength;

            if (!string.IsNullOrEmpty(m_redirectUrl))
            {
                cef_string_t.Copy(m_redirectUrl, redirectUrl);
            }
        }

        protected abstract void GetResponseHeaders(CefResponse response, out long responseLength, out string redirectUrl);

        private int skip(cef_resource_handler_t* self, long bytes_to_skip, long* bytes_skipped, cef_resource_skip_callback_t* callback)
        {
            CheckSelf(self);

            var m_callback = CefResourceSkipCallback.FromNative(callback);

            var m_result = Skip(bytes_to_skip, out var m_bytesSkipped, m_callback);

            *bytes_skipped = m_bytesSkipped;

            return m_result ? 1 : 0;
        }

        protected abstract bool Skip(long bytesToSkip, out long bytesSkipped, CefResourceSkipCallback callback);

        private int read(cef_resource_handler_t* self, void* data_out, int bytes_to_read, int* bytes_read, cef_resource_read_callback_t* callback)
        {
            CheckSelf(self);

            var m_callback = CefResourceReadCallback.FromNative(callback);

            using (var m_stream = new UnmanagedMemoryStream((byte*)data_out, bytes_to_read, bytes_to_read, FileAccess.Write))
            {
                var m_result = Read(m_stream, bytes_to_read, out var m_bytesRead, m_callback);
                *bytes_read = m_bytesRead;
                return m_result ? 1 : 0;
            }
        }

        protected abstract bool Read(Stream response, int bytesToRead, out int bytesRead, CefResourceReadCallback callback);

        private int read_response(cef_resource_handler_t* self, void* data_out, int bytes_to_read, int* bytes_read, cef_callback_t* callback)
        {
            CheckSelf(self);

            var m_callback = CefCallback.FromNative(callback);

            using (var m_stream = new UnmanagedMemoryStream((byte*)data_out, bytes_to_read, bytes_to_read, FileAccess.Write))
            {
                int m_bytesRead;
#pragma warning disable CS0618
                var result = ReadResponse(m_stream, bytes_to_read, out m_bytesRead, m_callback);
#pragma warning restore CS0618
                *bytes_read = m_bytesRead;
                return result ? 1 : 0;
            }
        }

        [Obsolete("This method is deprecated. Use Skip and Read instead.")]
        protected virtual bool ReadResponse(Stream response, int bytesToRead, out int bytesRead, CefCallback callback)
        {
            bytesRead = 0;
            return false;
        }

        private void cancel(cef_resource_handler_t* self)
        {
            CheckSelf(self);

            Cancel();
        }

        protected abstract void Cancel();
    }
}
