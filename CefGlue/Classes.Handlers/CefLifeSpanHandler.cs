namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefLifeSpanHandler
    {
        private int on_before_popup(cef_life_span_handler_t* self, cef_browser_t* browser, cef_frame_t* frame, int popup_id, cef_string_t* target_url, cef_string_t* target_frame_name, CefWindowOpenDisposition target_disposition, int user_gesture, cef_popup_features_t* popupFeatures, cef_window_info_t* windowInfo, cef_client_t** client, cef_browser_settings_t* settings, cef_dictionary_value_t** extra_info, int* no_javascript_access)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var m_frame = CefFrame.FromNative(frame);
            var m_targetUrl = cef_string_t.ToString(target_url);
            var m_targetFrameName = cef_string_t.ToString(target_frame_name);
            var m_userGesture = user_gesture != 0;
            var m_popupFeatures = new CefPopupFeatures(popupFeatures);
            var m_windowInfo = CefWindowInfo.FromNative(windowInfo);
            var m_client = CefClient.FromNative(*client);
            var m_settings = new CefBrowserSettings(settings);
            var m_extraInfo = CefDictionaryValue.FromNativeOrNull(*extra_info);
            var m_noJavascriptAccess = (*no_javascript_access) != 0;

            var o_extraInfo = m_extraInfo;
            var o_client = m_client;
            var result = OnBeforePopup(m_browser, m_frame, popup_id, m_targetUrl, m_targetFrameName, target_disposition, m_userGesture, m_popupFeatures, m_windowInfo, ref m_client, m_settings, ref m_extraInfo, ref m_noJavascriptAccess);

            if ((object)o_client != m_client && m_client != null)
            {
                *client = m_client.ToNative();
            }

            if ((object)o_extraInfo != m_extraInfo)
            {
                *extra_info = m_extraInfo != null ? m_extraInfo.ToNative() : null;
            }

            *no_javascript_access = m_noJavascriptAccess ? 1 : 0;

            m_popupFeatures.Dispose();
            m_windowInfo.Dispose();
            m_settings.Dispose();

            return result ? 1 : 0;
        }

        protected virtual bool OnBeforePopup(CefBrowser browser, CefFrame frame, int popupId, string targetUrl, string targetFrameName, CefWindowOpenDisposition targetDisposition, bool userGesture, CefPopupFeatures popupFeatures, CefWindowInfo windowInfo, ref CefClient client, CefBrowserSettings settings, ref CefDictionaryValue extraInfo, ref bool noJavascriptAccess)
        {
            return false;
        }

        private void on_after_created(cef_life_span_handler_t* self, cef_browser_t* browser)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            OnAfterCreated(m_browser);
        }

        protected virtual void OnAfterCreated(CefBrowser browser)
        {
        }

        private void on_before_popup_aborted(cef_life_span_handler_t* self, cef_browser_t* browser, int popup_id)
        {
            OnBeforePopupAborted(CefBrowser.FromNative(browser),popup_id);
        }

        protected virtual void OnBeforePopupAborted(CefBrowser browser, int popup_id)
        {
        }

        private void on_before_dev_tools_popup(cef_life_span_handler_t* self, cef_browser_t* browser, cef_window_info_t* windowInfo, cef_client_t** client, cef_browser_settings_t* settings, cef_dictionary_value_t** extra_info, int* use_default_window)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var m_windowInfo = CefWindowInfo.FromNative(windowInfo);
            var m_client = CefClient.FromNative(*client);
            var m_settings = new CefBrowserSettings(settings);
            var m_extraInfo = CefDictionaryValue.FromNativeOrNull(*extra_info);
            var m_useDefaultWindow = (*use_default_window) != 0;

            var o_extraInfo = m_extraInfo;
            var o_client = m_client;

            OnBeforeDevToolsPopup(m_browser, m_windowInfo, ref m_client, m_settings, ref m_extraInfo, ref m_useDefaultWindow);

            if ((object)o_client != m_client && m_client != null)
            {
                *client = m_client.ToNative();
            }

            if ((object)o_extraInfo != m_extraInfo)
            {
                *extra_info = m_extraInfo != null ? m_extraInfo.ToNative() : null;
            }

            *use_default_window = m_useDefaultWindow ? 1 : 0;

            m_windowInfo.Dispose();
            m_settings.Dispose();
        }

        protected virtual void OnBeforeDevToolsPopup(CefBrowser browser, CefWindowInfo windowInfo, ref CefClient client, CefBrowserSettings settings, ref CefDictionaryValue extraInfo, ref bool useDefaultWindow)
        {
        }

        private int do_close(cef_life_span_handler_t* self, cef_browser_t* browser)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);

            return DoClose(m_browser) ? 1 : 0;
        }

        protected virtual bool DoClose(CefBrowser browser)
        {
            return false;
        }

        private void on_before_close(cef_life_span_handler_t* self, cef_browser_t* browser)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);

            OnBeforeClose(m_browser);
        }

        protected virtual void OnBeforeClose(CefBrowser browser)
        {
        }
    }
}
