namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefJSDialogCallback
    {
        public void Continue(bool success, string userInput)
        {
            fixed (char* userInput_str = userInput)
            {
                var n_userInput = new cef_string_t(userInput_str, userInput != null ? userInput.Length : 0);

                cef_jsdialog_callback_t.cont(_self, success ? 1 : 0, &n_userInput);
            }
        }
    }
}
