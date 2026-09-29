namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefPrintDialogCallback
    {
        public void Continue(CefPrintSettings settings)
        {
            cef_print_dialog_callback_t.cont(_self, settings.ToNative());
        }

        public void Cancel()
        {
            cef_print_dialog_callback_t.cancel(_self);
        }
    }
}
