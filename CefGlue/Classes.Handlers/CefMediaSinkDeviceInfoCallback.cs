namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefMediaSinkDeviceInfoCallback
    {
        private void on_media_sink_device_info(cef_media_sink_device_info_callback_t* self, cef_media_sink_device_info_t* device_info)
        {
            CheckSelf(self);

            var mIPAddress = cef_string_t.ToString(&device_info->ip_address);
            var mModelName = cef_string_t.ToString(&device_info->model_name);

            var mDeviceInfo = new CefMediaSinkDeviceInfo(
                ipAddress: mIPAddress,
                port: device_info->port,
                modelName: mModelName);

            OnMediaSinkDeviceInfo(in mDeviceInfo);
        }

        protected abstract void OnMediaSinkDeviceInfo(in CefMediaSinkDeviceInfo deviceInfo);
    }
}
