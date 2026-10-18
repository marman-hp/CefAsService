namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefMediaObserver
    {
        private void on_sinks(cef_media_observer_t* self, UIntPtr sinksCount, cef_media_sink_t** sinks)
        {
            CheckSelf(self);

            var mSinksCount = checked((int)sinksCount);
            var mSinks = new CefMediaSink[mSinksCount];
            for (var i = 0; i < mSinksCount; i++)
            {
                mSinks[i] = CefMediaSink.FromNative(sinks[i]);
            }

            OnSinks(mSinks);
        }

        protected abstract void OnSinks(CefMediaSink[] sinks);

        private void on_routes(cef_media_observer_t* self, UIntPtr routesCount, cef_media_route_t** routes)
        {
            CheckSelf(self);

            var mRoutesCount = checked((int)routesCount);
            var mRoutes = new CefMediaRoute[mRoutesCount];
            for (var i = 0; i < mRoutesCount; i++)
            {
                mRoutes[i] = CefMediaRoute.FromNative(routes[i]);
            }

            OnRoutes(mRoutes);
        }

        protected abstract void OnRoutes(CefMediaRoute[] routes);

        private void on_route_state_changed(cef_media_observer_t* self, cef_media_route_t* route, CefMediaRouteConnectionState state)
        {
            CheckSelf(self);

            var mRoute = CefMediaRoute.FromNative(route);
            OnRouteStateChanged(mRoute, state);
        }

        protected abstract void OnRouteStateChanged(CefMediaRoute route, CefMediaRouteConnectionState state);

        private void on_route_message_received(cef_media_observer_t* self, cef_media_route_t* route, void* message, UIntPtr message_size)
        {
            CheckSelf(self);

            var mRoute = CefMediaRoute.FromNative(route);
            var mMessageSize = checked((int)message_size);

            OnRouteMessageReceived(mRoute, (IntPtr)message, mMessageSize);
        }

        protected abstract void OnRouteMessageReceived(CefMediaRoute route, IntPtr message, int messageSize);
    }
}
