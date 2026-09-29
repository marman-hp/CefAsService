namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefMediaRoute
    {
        public string Id
        {
            get
            {
                var n_result = cef_media_route_t.get_id(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public CefMediaSource GetSource()
        {
            return CefMediaSource.FromNative(
                cef_media_route_t.get_source(_self)
                );
        }

        public CefMediaSink GetSink()
        {
            return CefMediaSink.FromNative(
                cef_media_route_t.get_sink(_self)
                );
        }

        public void SendRouteMessage(IntPtr message, int messageSize)
        {
            var n_messageSize = checked((UIntPtr)messageSize);
            cef_media_route_t.send_route_message(_self, (void*)message, n_messageSize);
        }

        public void Terminate()
        {
            cef_media_route_t.terminate(_self);
        }
    }
}
