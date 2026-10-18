using System;

namespace Xilium.CefGlue
{
    [Flags]
    public enum CefTouchHandleStateFlags : uint
    {
        None = 0,
        Enabled = 1 << 0,
        Orientation = 1 << 1,
        Origin = 1 << 2,
        Alpha = 1 << 3,
    }
}
