namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefPermissionHandler
    {
        private int on_request_media_access_permission(cef_permission_handler_t* self, cef_browser_t* browser, cef_frame_t* frame, cef_string_t* requesting_origin, uint requested_permissions, cef_media_access_callback_t* callback)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            var mFrame = CefFrame.FromNative(frame);
            var mRequestingOrigin = cef_string_t.ToString(requesting_origin);
            var mRequestedPermissions = (CefMediaAccessPermissionTypes)requested_permissions;
            var mCallback = CefMediaAccessCallback.FromNative(callback);

            var result = OnRequestMediaAccessPermission(mBrowser, mFrame, mRequestingOrigin, mRequestedPermissions, mCallback);
            if (!result)
            {
                mCallback.Dispose();
            }
            return result ? 1 : 0;
        }

        protected virtual bool OnRequestMediaAccessPermission(CefBrowser browser, CefFrame frame,
            string requestingOrigin,
            CefMediaAccessPermissionTypes requestedPermissions,
            CefMediaAccessCallback callback)
            => false;

        private int on_show_permission_prompt(cef_permission_handler_t* self, cef_browser_t* browser, ulong prompt_id, cef_string_t* requesting_origin, uint requested_permissions, cef_permission_prompt_callback_t* callback)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            var mRequestingOrigin = cef_string_t.ToString(requesting_origin);
            var mRequestedPermissions = (CefPermissionRequestTypes)requested_permissions;
            var mCallback = CefPermissionPromptCallback.FromNative(callback);

            var result = OnShowPermissionPrompt(mBrowser, prompt_id, mRequestingOrigin,
                mRequestedPermissions, mCallback);
            if (!result)
            {
                mCallback.Dispose();
            }
            return result ? 1 : 0;
        }

        protected virtual bool OnShowPermissionPrompt(CefBrowser browser, ulong promptId, string requestingOrigin,
            CefPermissionRequestTypes requestedPermissions,
            CefPermissionPromptCallback callback)
            => false;

        private void on_dismiss_permission_prompt(cef_permission_handler_t* self, cef_browser_t* browser, ulong prompt_id, CefPermissionRequestResult result)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            OnDismissPermissionPrompt(mBrowser, prompt_id, result);
        }

        protected virtual void OnDismissPermissionPrompt(CefBrowser browser, ulong promptId, CefPermissionRequestResult result)
        { }
    }
}
