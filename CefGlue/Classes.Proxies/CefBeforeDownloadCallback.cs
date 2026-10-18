namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefBeforeDownloadCallback
    {
        public void Continue(string downloadPath, bool showDialog)
        {
            fixed (char* downloadPath_ptr = downloadPath)
            {
                var n_downloadPath = new cef_string_t(downloadPath_ptr, downloadPath != null ? downloadPath.Length : 0);
                cef_before_download_callback_t.cont(_self, &n_downloadPath, showDialog ? 1 : 0);
            }
        }
    }
}
