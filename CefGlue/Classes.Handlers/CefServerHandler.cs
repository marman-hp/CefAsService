namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefServerHandler
    {
        private void on_server_created(cef_server_handler_t* self, cef_server_t* server)
        {
            CheckSelf(self);

            var mServer = CefServer.FromNative(server);
            OnServerCreated(mServer);
        }

        protected abstract void OnServerCreated(CefServer server);

        private void on_server_destroyed(cef_server_handler_t* self, cef_server_t* server)
        {
            CheckSelf(self);

            var mServer = CefServer.FromNative(server);
            OnServerDestroyed(mServer);
        }

        protected abstract void OnServerDestroyed(CefServer server);

        private void on_client_connected(cef_server_handler_t* self, cef_server_t* server, int connection_id)
        {
            CheckSelf(self);

            var mServer = CefServer.FromNative(server);
            OnClientConnected(mServer, connection_id);
        }

        protected abstract void OnClientConnected(CefServer server, int connectionId);

        private void on_client_disconnected(cef_server_handler_t* self, cef_server_t* server, int connection_id)
        {
            CheckSelf(self);

            var mServer = CefServer.FromNative(server);
            OnClientDisconnected(mServer, connection_id);
        }

        protected abstract void OnClientDisconnected(CefServer server, int connectionId);

        private void on_http_request(cef_server_handler_t* self, cef_server_t* server, int connection_id, cef_string_t* client_address, cef_request_t* request)
        {
            CheckSelf(self);

            var mServer = CefServer.FromNative(server);
            var mClientAddress = cef_string_t.ToString(client_address);
            var mRequest = CefRequest.FromNative(request);

            OnHttpRequest(mServer, connection_id, mClientAddress, mRequest);
        }

        protected abstract void OnHttpRequest(CefServer server, int connectionId, string clientAddress, CefRequest request);

        private void on_web_socket_request(cef_server_handler_t* self, cef_server_t* server, int connection_id, cef_string_t* client_address, cef_request_t* request, cef_callback_t* callback)
        {
            CheckSelf(self);

            var mServer = CefServer.FromNative(server);
            var mClientAddress = cef_string_t.ToString(client_address);
            var mRequest = CefRequest.FromNative(request);
            var mCallback = CefCallback.FromNative(callback);

            OnWebSocketRequest(mServer, connection_id, mClientAddress, mRequest, mCallback);
        }

        protected abstract void OnWebSocketRequest(CefServer server, int connectionId, string clientAddress, CefRequest request, CefCallback callback);

        private void on_web_socket_connected(cef_server_handler_t* self, cef_server_t* server, int connection_id)
        {
            CheckSelf(self);

            var mServer = CefServer.FromNative(server);
            OnWebSocketConnected(mServer, connection_id);
        }

        protected abstract void OnWebSocketConnected(CefServer server, int connectionId);

        private void on_web_socket_message(cef_server_handler_t* self, cef_server_t* server, int connection_id, void* data, UIntPtr data_size)
        {
            CheckSelf(self);

            var mServer = CefServer.FromNative(server);
            var mData = (IntPtr)data;
            var mDataSize = checked((long)data_size);

            OnWebSocketMessage(mServer, connection_id, mData, mDataSize);
        }

        protected abstract void OnWebSocketMessage(CefServer server, int connectionId, IntPtr data, long dataSize);
    }
}
