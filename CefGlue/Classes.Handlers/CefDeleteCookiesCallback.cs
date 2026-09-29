namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefDeleteCookiesCallback
    {
        private void on_complete(cef_delete_cookies_callback_t* self, int num_deleted)
        {
            CheckSelf(self);
            OnComplete(num_deleted);
        }

        protected abstract void OnComplete(int numDeleted);
    }
}
