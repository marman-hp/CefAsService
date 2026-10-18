namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefRunFileDialogCallback
    {
        private void on_file_dialog_dismissed(cef_run_file_dialog_callback_t* self, cef_string_list* file_paths)
        {
            CheckSelf(self);

            var mFilePaths = cef_string_list.ToArray(file_paths);

            OnFileDialogDismissed(mFilePaths);
        }

        protected abstract void OnFileDialogDismissed(string[] filePaths);
    }
}
