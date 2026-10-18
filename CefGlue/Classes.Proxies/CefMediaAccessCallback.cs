namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefMediaAccessCallback
    {
        public void Continue(CefMediaAccessPermissionTypes allowedPermissions)
        {
            cef_media_access_callback_t.cont(_self, (uint)allowedPermissions);
        }

        public void Cancel()
        {
            cef_media_access_callback_t.cancel(_self);
        }
    }
}
