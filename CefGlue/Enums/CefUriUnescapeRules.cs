namespace Xilium.CefGlue
{
    using System;

    [Flags]
    public enum CefUriUnescapeRules
    {
        None = 0 << 0,

        Normal = 1 << 0,

        Spaces = 1 << 1,

        PathSeparators = 1 << 2,

        UrlSpecialCharsExceptPathSeparators = 1 << 3,

        ReplacePlusWithSpace = 1 << 4,
    }
}
