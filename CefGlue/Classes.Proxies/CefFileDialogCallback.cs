namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefFileDialogCallback
    {
        public void Continue(string[] filePaths)
        {
            var n_filePaths = cef_string_list.From(filePaths);

            cef_file_dialog_callback_t.cont(_self, n_filePaths);

            libcef.string_list_free(n_filePaths);
        }

        public void Cancel()
        {
            cef_file_dialog_callback_t.cancel(_self);
        }
    }
}
