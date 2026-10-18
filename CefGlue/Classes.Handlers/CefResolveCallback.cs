namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefResolveCallback
    {
        private void on_resolve_completed(cef_resolve_callback_t* self, CefErrorCode result, cef_string_list* resolved_ips)
        {
            CheckSelf(self);

            var mResolvedIps = cef_string_list.ToArray(resolved_ips);
            OnResolveCompleted(result, mResolvedIps);
        }

        protected abstract void OnResolveCompleted(CefErrorCode result, string[] resolvedIps);
    }
}
