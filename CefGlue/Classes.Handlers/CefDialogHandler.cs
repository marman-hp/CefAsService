namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefDialogHandler
    {
        private int on_file_dialog(cef_dialog_handler_t* self, cef_browser_t* browser, CefFileDialogMode mode, cef_string_t* title, cef_string_t* default_file_path, cef_string_list* accept_filters, cef_string_list* accept_extensions, cef_string_list* accept_descriptions, cef_file_dialog_callback_t* callback)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            var mTitle = cef_string_t.ToString(title);
            var mDefaultFilePath = cef_string_t.ToString(default_file_path);
            var mAcceptFilters = cef_string_list.ToArray(accept_filters);
            var mAcceptExtensions = cef_string_list.ToArray(accept_extensions);
            var mAcceptDescriptions = cef_string_list.ToArray(accept_descriptions);

            var mCallback = CefFileDialogCallback.FromNative(callback);

            var result = OnFileDialog(mBrowser, mode, mTitle, mDefaultFilePath, mAcceptFilters, mAcceptExtensions, mAcceptDescriptions, mCallback);
            return result ? 1 : 0;
        }

        protected virtual bool OnFileDialog(CefBrowser browser, CefFileDialogMode mode, string title, string defaultFilePath, string[] acceptFilters, string[] acceptExtensions, string[] acceptDescriptions, CefFileDialogCallback callback)
            => false;
    }
}
