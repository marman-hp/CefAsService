namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefMediaSink
    {
        public string Id
        {
            get
            {
                var n_result = cef_media_sink_t.get_id(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string Name
        {
            get
            {
                var n_result = cef_media_sink_t.get_name(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public CefMediaSinkIconType IconType =>
            cef_media_sink_t.get_icon_type(_self);

        public void GetDeviceInfo(CefMediaSinkDeviceInfoCallback callback)
        {
            cef_media_sink_t.get_device_info(_self, callback.ToNative());
        }

        public bool IsCastSink => cef_media_sink_t.is_cast_sink(_self) != 0;

        public bool IsDialSink => cef_media_sink_t.is_dial_sink(_self) != 0;

        public bool IsCompatibleWith(CefMediaSource source)
        {
            return cef_media_sink_t.is_compatible_with(_self, source.ToNative()) != 0;
        }
    }
}
