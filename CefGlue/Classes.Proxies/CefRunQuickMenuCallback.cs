namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefRunQuickMenuCallback
    {
        public void Continue(int commandId, CefEventFlags eventFlags)
        {
            cef_run_quick_menu_callback_t.cont(_self, commandId, eventFlags);
        }

        public void Cancel()
        {
            cef_run_quick_menu_callback_t.cancel(_self);
        }
    }
}
