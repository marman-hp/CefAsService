using System;

namespace Xilium.CefGlue
{
    [Flags]
    public enum CefQuickMenuEditStateFlags
    {
        None = 0,
        CanEllipsis = 1 << 0,
        CanCut = 1 << 1,
        CanCopy = 1 << 2,
        CanPaste = 1 << 3,
    }
}
