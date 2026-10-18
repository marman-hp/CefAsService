namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefDownloadHandler
    {
        private int can_download(cef_download_handler_t* self, cef_browser_t* browser, cef_string_t* url, cef_string_t* request_method)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var m_url = cef_string_t.ToString(url);
            var m_requestMethod = cef_string_t.ToString(request_method);
            return CanDownload(m_browser, m_url, m_requestMethod) ? 1 : 0;
        }

        protected virtual bool CanDownload(CefBrowser browser, string url, string requestMethod) => true;

        private int on_before_download(cef_download_handler_t* self, cef_browser_t* browser, cef_download_item_t* download_item, cef_string_t* suggested_name, cef_before_download_callback_t* callback)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            using (var m_download_item = CefDownloadItem.FromNative(download_item))
            {
                var m_suggested_name = cef_string_t.ToString(suggested_name);
                var m_callback = CefBeforeDownloadCallback.FromNative(callback);

                return OnBeforeDownload(m_browser, m_download_item, m_suggested_name, m_callback) ? 1 : 0;
            }
        }

        protected virtual bool OnBeforeDownload(CefBrowser browser, CefDownloadItem downloadItem, string suggestedName, CefBeforeDownloadCallback callback)
        {
            return false;
        }

        private void on_download_updated(cef_download_handler_t* self, cef_browser_t* browser, cef_download_item_t* download_item, cef_download_item_callback_t* callback)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            using (var m_download_item = CefDownloadItem.FromNative(download_item))
            {
                var m_callback = CefDownloadItemCallback.FromNative(callback);

                OnDownloadUpdated(m_browser, m_download_item, m_callback);
            }
        }

        protected virtual void OnDownloadUpdated(CefBrowser browser, CefDownloadItem downloadItem, CefDownloadItemCallback callback)
        {
        }
    }
}
