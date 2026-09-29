namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefFrameHandler
    {
        private void on_frame_created(cef_frame_handler_t* self, cef_browser_t* browser, cef_frame_t* frame)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            var mFrame = CefFrame.FromNative(frame);
            OnFrameCreated(mBrowser, mFrame);
        }

        protected virtual void OnFrameCreated(CefBrowser browser, CefFrame frame)
        {
        }

        private void on_frame_destroyed(cef_frame_handler_t* self, cef_browser_t* browser, cef_frame_t* frame)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            var mFrame = CefFrame.FromNative(frame);
            OnFrameDestroyed(mBrowser, mFrame);
        }

        protected virtual void OnFrameDestroyed(CefBrowser browser, CefFrame frame)
        {
        }

        private void on_frame_attached(cef_frame_handler_t* self, cef_browser_t* browser, cef_frame_t* frame, int reattached)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            var mFrame = CefFrame.FromNative(frame);
            OnFrameAttached(mBrowser, mFrame, reattached != 0);
        }

        protected virtual void OnFrameAttached(CefBrowser browser, CefFrame frame, bool reattached)
        { }

        private void on_frame_detached(cef_frame_handler_t* self, cef_browser_t* browser, cef_frame_t* frame)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            var mFrame = CefFrame.FromNative(frame);
            OnFrameDetached(mBrowser, mFrame);
        }

        protected virtual void OnFrameDetached(CefBrowser browser, CefFrame frame)
        { }

        private void on_main_frame_changed(cef_frame_handler_t* self, cef_browser_t* browser, cef_frame_t* old_frame, cef_frame_t* new_frame)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            var mOldFrame = CefFrame.FromNativeOrNull(old_frame);
            var mNewFrame = CefFrame.FromNativeOrNull(new_frame);
            OnMainFrameChanged(mBrowser, mOldFrame, mNewFrame);
        }

        protected virtual void OnMainFrameChanged(CefBrowser browser, CefFrame? oldFrame, CefFrame? newFrame)
        { }
    }
}
