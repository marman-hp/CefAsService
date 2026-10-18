namespace Xilium.CefGlue
{
    using System;

    [Flags]
    public enum CefDragOperationsMask : uint
    {
        None = 0,
        Copy = 1,
        Link = 2,
        Generic = 4,
        Private = 8,
        Move = 16,
        Delete = 32,
        Every = UInt32.MaxValue,
    }
}
