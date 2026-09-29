namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefResourceReadCallback
    {
        public void Continue(int bytesRead)
        {
            cef_resource_read_callback_t.cont(_self, bytesRead);
        }
    }
}
