namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefSetCookieCallback
    {
        private void on_complete(cef_set_cookie_callback_t* self, int success)
        {
            CheckSelf(self);
            OnComplete(success != 0);
        }

        protected abstract void OnComplete(bool success);
    }
}
