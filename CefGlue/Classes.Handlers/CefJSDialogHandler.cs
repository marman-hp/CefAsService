namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefJSDialogHandler
    {
        private int on_jsdialog(cef_jsdialog_handler_t* self, cef_browser_t* browser, cef_string_t* origin_url, CefJSDialogType dialog_type, cef_string_t* message_text, cef_string_t* default_prompt_text, cef_jsdialog_callback_t* callback, int* suppress_message)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var m_origin_url = cef_string_t.ToString(origin_url);
            var m_message_text = cef_string_t.ToString(message_text);
            var m_default_prompt_text = cef_string_t.ToString(default_prompt_text);
            var m_callback = CefJSDialogCallback.FromNative(callback);
            bool m_suppress_message;

            var result = OnJSDialog(m_browser, m_origin_url, dialog_type, m_message_text, m_default_prompt_text, m_callback, out m_suppress_message);

            *suppress_message = m_suppress_message ? 1 : 0;
            return result ? 1 : 0;
        }

        protected abstract bool OnJSDialog(CefBrowser browser, string originUrl, CefJSDialogType dialogType, string message_text, string default_prompt_text, CefJSDialogCallback callback, out bool suppress_message);

        private int on_before_unload_dialog(cef_jsdialog_handler_t* self, cef_browser_t* browser, cef_string_t* message_text, int is_reload, cef_jsdialog_callback_t* callback)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var m_message_text = cef_string_t.ToString(message_text);
            var m_callback = CefJSDialogCallback.FromNative(callback);

            return OnBeforeUnloadDialog(m_browser, m_message_text, is_reload != 0, m_callback) ? 1 : 0;
        }

        protected abstract bool OnBeforeUnloadDialog(CefBrowser browser, string messageText, bool isReload, CefJSDialogCallback callback);

        private void on_reset_dialog_state(cef_jsdialog_handler_t* self, cef_browser_t* browser)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);

            OnResetDialogState(m_browser);
        }

        protected abstract void OnResetDialogState(CefBrowser browser);

        private void on_dialog_closed(cef_jsdialog_handler_t* self, cef_browser_t* browser)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);

            OnDialogClosed(m_browser);
        }

        protected abstract void OnDialogClosed(CefBrowser browser);
    }
}
