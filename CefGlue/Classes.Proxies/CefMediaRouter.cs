namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefMediaRouter
    {
        public static CefMediaRouter GetGlobalMediaRouter(CefCompletionCallback? callback)
        {
            var nCallback = callback != null ? callback.ToNative() : null;
            var nResult = cef_media_router_t.get_global(nCallback);
            return CefMediaRouter.FromNative(nResult);
        }

        public CefRegistration AddObserver(CefMediaObserver observer)
        {
            var n_result = cef_media_router_t.add_observer(_self, observer.ToNative());
            return CefRegistration.FromNative(n_result);
        }

        public CefMediaSource GetSource(string urn)
        {
            fixed (char* urn_str = urn)
            {
                var n_urn = new cef_string_t(urn_str, urn.Length);
                var n_result = cef_media_router_t.get_source(_self, &n_urn);
                return CefMediaSource.FromNativeOrNull(n_result);
            }
        }

        public void NotifyCurrentSinks()
        {
            cef_media_router_t.notify_current_sinks(_self);
        }

        public void CreateRoute(CefMediaSource source, CefMediaSink sink, CefMediaRouteCreateCallback callback)
        {
            cef_media_router_t.create_route(_self,
                source.ToNative(),
                sink.ToNative(),
                callback.ToNative());
        }

        public void NotifyCurrentRoutes()
        {
            cef_media_router_t.notify_current_routes(_self);
        }
    }
}
