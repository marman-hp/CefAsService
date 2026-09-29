namespace Xilium.CefGlue
{
    using System;

    [Flags]
    public enum CefV8PropertyAttribute
    {
        None = 0,

        ReadOnly = 1 << 0,

        DontEnum = 1 << 1,

        DontDelete = 1 << 2,
    }
}
