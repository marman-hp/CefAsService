namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefDownloadItemCallback
    {
        public void Cancel()
        {
            cef_download_item_callback_t.cancel(_self);
        }

        public void Pause()
        {
            cef_download_item_callback_t.pause(_self);
        }

        public void Resume()
        {
            cef_download_item_callback_t.resume(_self);
        }
    }
}
