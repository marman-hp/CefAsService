namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefResourceSkipCallback
    {
        public void Continue(long bytesSkipped)
        {
            cef_resource_skip_callback_t.cont(_self, bytesSkipped);
        }
    }
}
