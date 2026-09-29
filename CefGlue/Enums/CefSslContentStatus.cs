namespace Xilium.CefGlue
{
    using System;

    [Flags]
    public enum CefSslContentStatus
    {
        Normal = 0,
        DisplayedInsecure = 1 << 0,
        RanInsecure = 1 << 1,
    }
}
