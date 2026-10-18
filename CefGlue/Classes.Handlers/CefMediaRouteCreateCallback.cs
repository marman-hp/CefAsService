namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefMediaRouteCreateCallback
    {
        private void on_media_route_create_finished(cef_media_route_create_callback_t* self, CefMediaRouteCreateResult result, cef_string_t* error, cef_media_route_t* route)
        {
            CheckSelf(self);

            var mError = cef_string_t.ToString(error);
            var mRoute = CefMediaRoute.FromNativeOrNull(route);

            OnMediaRouteCreateFinished(result, mError, mRoute);
        }

        protected abstract void OnMediaRouteCreateFinished(CefMediaRouteCreateResult result, string error, CefMediaRoute route);
    }
}
