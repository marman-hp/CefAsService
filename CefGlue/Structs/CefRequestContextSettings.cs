namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Xilium.CefGlue.Interop;

    public sealed class CefRequestContextSettings
    {
        public string CachePath { get; set; }

        public bool PersistSessionCookies { get; set; }

        public string AcceptLanguageList { get; set; }

        public string CookieableSchemesList { get; set; }

        public bool CookieableSchemesExcludeDefaults { get; set; }

        internal unsafe cef_request_context_settings_t* ToNative()
        {
            var ptr = cef_request_context_settings_t.Alloc();
            cef_string_t.Copy(CachePath, &ptr->cache_path);
            ptr->persist_session_cookies = PersistSessionCookies ? 1 : 0;
            cef_string_t.Copy(AcceptLanguageList, &ptr->accept_language_list);
            cef_string_t.Copy(CookieableSchemesList, &ptr->cookieable_schemes_list);
            ptr->cookieable_schemes_exclude_defaults = CookieableSchemesExcludeDefaults ? 1 : 0;
            return ptr;
        }

        private static unsafe void Clear(cef_request_context_settings_t* ptr)
        {
            libcef.string_clear(&ptr->cache_path);
            libcef.string_clear(&ptr->accept_language_list);
            libcef.string_clear(&ptr->cookieable_schemes_list);
        }

        internal static unsafe void Free(cef_request_context_settings_t* ptr)
        {
            Clear(ptr);
            cef_request_context_settings_t.Free(ptr);
        }
    }
}
