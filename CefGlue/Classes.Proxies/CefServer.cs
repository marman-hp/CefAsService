namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefServer
    {
        public static void Create(string address, ushort port, int backlog, CefServerHandler handler)
        {
            fixed (char* address_str = address)
            {
                var n_address = new cef_string_t(address_str, address.Length);
                cef_server_t.create(&n_address, port, backlog, handler.ToNative());
            }
        }

        public CefTaskRunner GetTaskRunner()
        {
            return CefTaskRunner.FromNative(
                cef_server_t.get_task_runner(_self)
                );
        }

        public void Shutdown()
        {
            cef_server_t.shutdown(_self);
        }

        public bool IsRunning
        {
            get
            {
                return cef_server_t.is_running(_self) != 0;
            }
        }

        public string Address
        {
            get
            {
                return cef_string_userfree.ToString(
                    cef_server_t.get_address(_self)
                    );
            }
        }

        public bool HasConnection
        {
            get
            {
                return cef_server_t.has_connection(_self) != 0;
            }
        }

        public bool IsValidConnection(int connectionId)
        {
            return cef_server_t.is_valid_connection(_self, connectionId) != 0;
        }

        public void SendHttp200Response(int connectionId, string contentType, IntPtr data, long dataSize)
        {
            fixed (char* contentType_str = contentType)
            {
                var n_contentType = new cef_string_t(contentType_str, contentType != null ? contentType.Length : 0);
                var n_dataSize = checked((UIntPtr)dataSize);
                cef_server_t.send_http200_response(_self, connectionId, &n_contentType, (void*)data, n_dataSize);
            }
        }

        public void SendHttp404Response(int connectionId)
        {
            cef_server_t.send_http404_response(_self, connectionId);
        }

        public void SendHttp500Response(int connectionId, string errorMessage)
        {
            fixed (char* errorMessage_str = errorMessage)
            {
                var n_errorMessage = new cef_string_t(errorMessage_str, errorMessage != null ? errorMessage.Length : 0);
                cef_server_t.send_http500_response(_self, connectionId, &n_errorMessage);
            }
        }

        public void SendHttpResponse(int connectionId, int responseCode, string contentType, long contentLength, NameValueCollection extraHeaders)
        {
            fixed(char* contentType_str = contentType)
            {
                var n_contentType = new cef_string_t(contentType_str, contentType != null ? contentType.Length : 0);
                var n_extraHeaders = cef_string_multimap.From(extraHeaders);
                cef_server_t.send_http_response(_self, connectionId, responseCode, &n_contentType, contentLength, n_extraHeaders);
                libcef.string_multimap_free(n_extraHeaders);
            }
        }

        public void SendRawData(int connectionId, IntPtr data, long dataSize)
        {
            var n_dataSize = checked((UIntPtr)dataSize);
            cef_server_t.send_raw_data(_self, connectionId, (void*)data, n_dataSize);
        }

        public void CloseConnection(int connectionId)
        {
            cef_server_t.close_connection(_self, connectionId);
        }

        public void SendWebSocketMessage(int connectionId, IntPtr data, long dataSize)
        {
            var n_dataSize = checked((UIntPtr)dataSize);
            cef_server_t.send_web_socket_message(_self, connectionId, (void*)data, n_dataSize);
        }
    }
}
