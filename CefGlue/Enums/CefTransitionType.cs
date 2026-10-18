namespace Xilium.CefGlue
{
    using System;

    [Flags]
    public enum CefTransitionType : uint
    {
        Link,

        Explicit,

        AutoBookmark,

        AutoSubframe,

        ManualSubframe,

        Generated,

        AutoTopLevel,

        FormSubmit,

        Reload,

        Keyword,

        KeywordGenerated,

        SourceMask = 0xFF,

        BlockedFlag = 0x00800000,

        ForwardBackFlag = 0x01000000,

        DirectLoadFlag = 0x02000000,

        HomePageFlag = 0x04000000,

        FromApiFlag = 0x08000000,

        ChainStartFlag = 0x10000000,

        ChainEndFlag = 0x20000000,

        ClientRedirectFlag = 0x40000000,

        ServerRedirectFlag = 0x80000000,

        IsRedirectMask = 0xC0000000,

        QualifierMask = 0xFFFFFF00,
    }
}
