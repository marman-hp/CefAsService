namespace Xilium.CefGlue
{
    using System;

    [Flags]
    public enum CefJsonParserOptions
    {
        Rfc = 0,

        AllowTrailingCommas = 1 << 0,
    }
}
