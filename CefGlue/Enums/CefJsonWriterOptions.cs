namespace Xilium.CefGlue
{
    using System;

    [Flags]
    public enum CefJsonWriterOptions
    {
        Default = 0,

        OmitBinaryValues = 1 << 0,

        OmitDoubleTypePreservation = 1 << 1,

        PrettyPrint = 1 << 2,
    }
}
