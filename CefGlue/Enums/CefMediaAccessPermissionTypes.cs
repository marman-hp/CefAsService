namespace Xilium.CefGlue
{
    using System;

    [Flags]
    public enum CefMediaAccessPermissionTypes
    {
        None = 0,

        DeviceAudioCapture = 1 << 0,

        DeviceVideoCapture = 1 << 1,

        DesktopAudioCapture = 1 << 2,

        DesktopVideoCapture = 1 << 3,
    }
}
