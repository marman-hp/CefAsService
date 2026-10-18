namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefPermissionPromptCallback
    {
        public void Continue(CefPermissionRequestResult result)
        {
            cef_permission_prompt_callback_t.cont(_self, result);
        }
    }
}
