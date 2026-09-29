using System;

namespace Xilium.CefGlue.Headless
{
    public interface IZeroCopyEncoderProbe
    {
        bool TryProbe(IntPtr d3d11Device, out string reason);
    }
}
