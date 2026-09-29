namespace Xilium.CefGlue.Interop
{
    using System;
    using System.Runtime.InteropServices;
    using System.Diagnostics.CodeAnalysis;

    internal static unsafe partial class libcef
    {
        public const string CEF_VERSION = "147.0.14+g76d2442+chromium-147.0.7727.138";
        public const int CEF_VERSION_MAJOR = 147;
        public const int CEF_COMMIT_NUMBER = 3516;
        public const string CEF_COMMIT_HASH = "76d244268947a52f43755983ef83766a353a1335";

        public const int CHROME_VERSION_MAJOR = 147;
        public const int CHROME_VERSION_MINOR = 0;
        public const int CHROME_VERSION_BUILD = 7727;
        public const int CHROME_VERSION_PATCH = 138;

        public const int CEF_API_VERSION = 14700;
        public const string CEF_API_HASH_PLATFORM_WIN = "1cf33dbf355efffcd993ab4d2ea391e6631f95f2";
        public const string CEF_API_HASH_PLATFORM_MACOS = "4b5130ce2abe48970a3edf9b3d2e541c007f6938";
        public const string CEF_API_HASH_PLATFORM_LINUX = "ca03ee7aa4d9a766d43302c03689684de0b78966";
    }
}
