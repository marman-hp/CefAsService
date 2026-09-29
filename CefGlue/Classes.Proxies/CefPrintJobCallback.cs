namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefPrintJobCallback
    {
        public void Continue()
        {
            cef_print_job_callback_t.cont(_self);
        }
    }
}
