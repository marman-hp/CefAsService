namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefCookieManager
    {
        public static CefCookieManager GetGlobal(CefCompletionCallback callback)
        {
            var n_callback = callback != null ? callback.ToNative() : null;
            return CefCookieManager.FromNativeOrNull(
                cef_cookie_manager_t.get_global_manager(n_callback)
                );
        }

        public bool VisitAllCookies(CefCookieVisitor visitor)
        {
            if (visitor == null) throw new ArgumentNullException("visitor");

            return cef_cookie_manager_t.visit_all_cookies(_self, visitor.ToNative()) != 0;
        }

        public bool VisitUrlCookies(string url, bool includeHttpOnly, CefCookieVisitor visitor)
        {
            if (string.IsNullOrEmpty(url)) throw new ArgumentNullException("url");
            if (visitor == null) throw new ArgumentNullException("visitor");

            fixed (char* url_str = url)
            {
                var n_url = new cef_string_t(url_str, url.Length);

                return cef_cookie_manager_t.visit_url_cookies(_self, &n_url, includeHttpOnly ? 1 : 0, visitor.ToNative()) != 0;
            }
        }

        public bool SetCookie(string url, CefCookie cookie, CefSetCookieCallback callback)
        {
            if (string.IsNullOrEmpty(url)) throw new ArgumentNullException("url");
            if (cookie == null) throw new ArgumentNullException("cookie");

            int n_result;
            var n_cookie = cookie.ToNative();
            fixed (char* url_str = url)
            {
                var n_url = new cef_string_t(url_str, url.Length);
                var n_callback = callback != null ? callback.ToNative() : null;

                n_result = cef_cookie_manager_t.set_cookie(_self, &n_url, n_cookie, n_callback);
            }
            CefCookie.Free(n_cookie);
            return n_result != 0;
        }

        public bool DeleteCookies(string url, string cookieName, CefDeleteCookiesCallback callback)
        {
            fixed (char* url_str = url)
            fixed (char* cookieName_str = cookieName)
            {
                var n_url = new cef_string_t(url_str, url != null ? url.Length : 0);
                var n_cookieName = new cef_string_t(cookieName_str, cookieName != null ? cookieName.Length : 0);
                var n_callback = callback != null ? callback.ToNative() : null;

                return cef_cookie_manager_t.delete_cookies(_self, &n_url, &n_cookieName, n_callback) != 0;
            }
        }

        public bool FlushStore(CefCompletionCallback callback)
        {
            var n_handler = callback != null ? callback.ToNative() : null;

            return cef_cookie_manager_t.flush_store(_self, n_handler) != 0;
        }
    }
}
