namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefCallback
    {
        public void Continue()
        {
            cef_callback_t.cont(_self);
        }

        public void Cancel()
        {
            cef_callback_t.cancel(_self);
        }
    }
}
