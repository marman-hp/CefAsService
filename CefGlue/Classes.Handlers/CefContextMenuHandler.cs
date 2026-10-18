namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefContextMenuHandler
    {
        private void on_before_context_menu(cef_context_menu_handler_t* self, cef_browser_t* browser, cef_frame_t* frame, cef_context_menu_params_t* @params, cef_menu_model_t* model)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            var mFrame = CefFrame.FromNative(frame);
            using (var mState = CefContextMenuParams.FromNative(@params))
            using (var mModel = CefMenuModel.FromNative(model))
            {
                OnBeforeContextMenu(mBrowser, mFrame, mState, mModel);
            }
        }

        protected virtual void OnBeforeContextMenu(CefBrowser browser, CefFrame frame, CefContextMenuParams state, CefMenuModel model)
        {
        }

        private int run_context_menu(cef_context_menu_handler_t* self, cef_browser_t* browser, cef_frame_t* frame, cef_context_menu_params_t* @params, cef_menu_model_t* model, cef_run_context_menu_callback_t* callback)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            var mFrame = CefFrame.FromNative(frame);
            using (var mParameters = CefContextMenuParams.FromNative(@params))
            using (var mModel = CefMenuModel.FromNative(model))
            {
                var mCallback = CefRunContextMenuCallback.FromNative(callback);
                var result = RunContextMenu(mBrowser, mFrame, mParameters, mModel, mCallback);

                return result ? 1 : 0;
            }
        }

        protected virtual bool RunContextMenu(CefBrowser browser, CefFrame frame, CefContextMenuParams parameters, CefMenuModel model, CefRunContextMenuCallback callback)
        {
            return false;
        }

        private int on_context_menu_command(cef_context_menu_handler_t* self, cef_browser_t* browser, cef_frame_t* frame, cef_context_menu_params_t* @params, int command_id, CefEventFlags event_flags)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            var mFrame = CefFrame.FromNative(frame);
            using (var mState = CefContextMenuParams.FromNative(@params))
            {
                var result = OnContextMenuCommand(mBrowser, mFrame, mState, command_id, event_flags);
                return result ? 1 : 0;
            }
        }

        protected virtual bool OnContextMenuCommand(CefBrowser browser, CefFrame frame, CefContextMenuParams state, int commandId, CefEventFlags eventFlags)
        {
            return false;
        }

        private void on_context_menu_dismissed(cef_context_menu_handler_t* self, cef_browser_t* browser, cef_frame_t* frame)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            var mFrame = CefFrame.FromNative(frame);

            OnContextMenuDismissed(mBrowser, mFrame);
        }

        protected virtual void OnContextMenuDismissed(CefBrowser browser, CefFrame frame)
        {
        }

        private int run_quick_menu(cef_context_menu_handler_t* self, cef_browser_t* browser, cef_frame_t* frame, cef_point_t* location, cef_size_t* size, CefQuickMenuEditStateFlags edit_state_flags, cef_run_quick_menu_callback_t* callback)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            var mFrame = CefFrame.FromNative(frame);
            var mLocation = new CefPoint(location->x, location->y);
            var mSize = new CefSize(size->width, size->height);
            var mCallback = CefRunQuickMenuCallback.FromNative(callback);

            var result = RunQuickMenu(mBrowser, mFrame, mLocation, mSize, edit_state_flags, mCallback);
            if (!result)
            {
                mCallback.Dispose();
            }
            return result ? 1 : 0;
        }

        protected virtual bool RunQuickMenu(CefBrowser browser, CefFrame frame, CefPoint location, CefSize size, CefQuickMenuEditStateFlags editStateFlags, CefRunQuickMenuCallback callback)
            => false;

        private int on_quick_menu_command(cef_context_menu_handler_t* self, cef_browser_t* browser, cef_frame_t* frame, int command_id, CefEventFlags event_flags)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            var mFrame = CefFrame.FromNative(frame);

            var result = OnQuickMenuCommand(mBrowser, mFrame, command_id, event_flags);
            return result ? 1 : 0;
        }

        protected virtual bool OnQuickMenuCommand(CefBrowser browser, CefFrame frame, int commandId, CefEventFlags eventFlags)
            => false;

        private void on_quick_menu_dismissed(cef_context_menu_handler_t* self, cef_browser_t* browser, cef_frame_t* frame)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            var mFrame = CefFrame.FromNative(frame);

            OnQuickMenuDismissed(mBrowser, mFrame);
        }

        protected virtual void OnQuickMenuDismissed(CefBrowser browser, CefFrame frame)
        { }
    }
}
