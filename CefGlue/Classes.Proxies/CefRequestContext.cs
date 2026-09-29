namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefRequestContext
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private cef_request_context_t* GetSelf()
            => (cef_request_context_t*)_self;

        public static CefRequestContext GetGlobalContext()
        {
            return CefRequestContext.FromNative(
                cef_request_context_t.get_global_context()
                );
        }

        public static CefRequestContext CreateContext(CefRequestContextSettings settings, CefRequestContextHandler handler)
        {
            var n_settings = settings.ToNative();

            var result = CefRequestContext.FromNative(
                cef_request_context_t.create_context(
                    n_settings,
                    handler != null ? handler.ToNative() : null
                    )
                );

            CefRequestContextSettings.Free(n_settings);

            return result;
        }

        public static CefRequestContext CreateContext(CefRequestContext other, CefRequestContextHandler handler)
        {
            return CefRequestContext.FromNative(
                cef_request_context_t.create_context(
                    other.ToNative(),
                    handler != null ? handler.ToNative() : null
                    )
                );
        }

        public bool IsSame(CefRequestContext other)
        {
            if (other == null) return false;

            return cef_request_context_t.is_same(GetSelf(), other.ToNative()) != 0;
        }

        public bool IsSharingWith(CefRequestContext other)
        {
            return cef_request_context_t.is_sharing_with(GetSelf(), other.ToNative()) != 0;
        }

        public bool IsGlobal
        {
            get
            {
                return cef_request_context_t.is_global(GetSelf()) != 0;
            }
        }

        public CefRequestContextHandler GetHandler()
        {
            return CefRequestContextHandler.FromNativeOrNull(
                cef_request_context_t.get_handler(GetSelf())
                );
        }

        public string CachePath
        {
            get
            {
                var n_result = cef_request_context_t.get_cache_path(GetSelf());
                return cef_string_userfree.ToString(n_result);
            }
        }

        public CefCookieManager GetCookieManager(CefCompletionCallback callback)
        {
            var n_callback = callback != null ? callback.ToNative() : null;

            return CefCookieManager.FromNativeOrNull(
                cef_request_context_t.get_cookie_manager(GetSelf(), n_callback)
                );
        }

        public bool RegisterSchemeHandlerFactory(string schemeName, string domainName, CefSchemeHandlerFactory factory)
        {
            if (string.IsNullOrEmpty(schemeName)) throw new ArgumentNullException("schemeName");
            if (factory == null) throw new ArgumentNullException("factory");

            fixed (char* schemeName_str = schemeName)
            fixed (char* domainName_str = domainName)
            {
                var n_schemeName = new cef_string_t(schemeName_str, schemeName.Length);
                var n_domainName = new cef_string_t(domainName_str, domainName != null ? domainName.Length : 0);

                return cef_request_context_t.register_scheme_handler_factory(GetSelf(), &n_schemeName, &n_domainName, factory.ToNative()) != 0;
            }
        }

        public bool ClearSchemeHandlerFactories()
        {
            return cef_request_context_t.clear_scheme_handler_factories(GetSelf()) != 0;
        }

        public void ClearCertificateExceptions(CefCompletionCallback callback)
        {
            var n_callback = callback != null ? callback.ToNative() : null;
            cef_request_context_t.clear_certificate_exceptions(GetSelf(), n_callback);
        }

        public void ClearHttpAuthCredentials(CefCompletionCallback callback)
        {
            var n_callback = callback != null ? callback.ToNative() : null;
            cef_request_context_t.clear_http_auth_credentials(GetSelf(), n_callback);
        }

        public void CloseAllConnections(CefCompletionCallback callback)
        {
            var n_callback = callback != null ? callback.ToNative() : null;
            cef_request_context_t.close_all_connections(GetSelf(), n_callback);
        }

        public void ResolveHost(string origin, CefResolveCallback callback)
        {
            if (string.IsNullOrEmpty(origin)) throw new ArgumentNullException("origin");
            if (callback == null) throw new ArgumentNullException("callback");

            fixed (char* origin_str = origin)
            {
                var n_origin = new cef_string_t(origin_str, origin != null ? origin.Length : 0);
                var n_callback = callback.ToNative();
                cef_request_context_t.resolve_host(GetSelf(), &n_origin, n_callback);
            }
        }

        public CefMediaRouter GetMediaRouter(CefCompletionCallback? callback)
        {
            var nCallback = callback != null ? callback.ToNative() : null;
            var nResult = cef_request_context_t.get_media_router(GetSelf(), nCallback);
            return CefMediaRouter.FromNative(nResult);
        }

        public CefValue GetWebsiteSettings(
            string requestingUrl,
            string topLevelUrl,
            CefContentSettingType contentType)
        {
            fixed (char* requestingUrl_str = requestingUrl)
            fixed (char* topLevelUrl_str = topLevelUrl)
            {
                var n_requestingUrl = new cef_string_t(requestingUrl_str, requestingUrl != null ? requestingUrl.Length : 0);
                var n_topLevelUrl = new cef_string_t(topLevelUrl_str, topLevelUrl != null ? topLevelUrl.Length : 0);

                var n_result = cef_request_context_t.get_website_setting(GetSelf(), &n_requestingUrl, &n_topLevelUrl, contentType);

                return CefValue.FromNativeOrNull(n_result);
            }
        }

        public void SetWebsiteSettings(
                 string requestingUrl,
                string topLevelUrl,
                CefContentSettingType contentType,
                CefValue value)
        {
            fixed (char* requestingUrl_str = requestingUrl)
            fixed (char* topLevelUrl_str = topLevelUrl)
            {
                var n_requestingUrl = new cef_string_t(requestingUrl_str, requestingUrl != null ? requestingUrl.Length : 0);
                var n_topLevelUrl = new cef_string_t(topLevelUrl_str, topLevelUrl != null ? topLevelUrl.Length : 0);
                var n_value = value.ToNative();

                cef_request_context_t.set_website_setting(GetSelf(), &n_requestingUrl, &n_topLevelUrl, contentType, n_value);
            }
        }

        public CefContentSettingValue GetContentSetting(
            string requestingUrl,
            string topLevelUrl,
            CefContentSettingType contentType)
        {
            fixed (char* requestingUrl_str = requestingUrl)
            fixed (char* topLevelUrl_str = topLevelUrl)
            {
                var n_requestingUrl = new cef_string_t(requestingUrl_str, requestingUrl != null ? requestingUrl.Length : 0);
                var n_topLevelUrl = new cef_string_t(topLevelUrl_str, topLevelUrl != null ? topLevelUrl.Length : 0);

                return cef_request_context_t.get_content_setting(GetSelf(), &n_requestingUrl, &n_topLevelUrl, contentType);
            }
        }

        public void setContentSetting(
                string requestingUrl,
                string topLevelUrl,
                CefContentSettingType contentType,
                CefContentSettingValue value)
        {
            fixed (char* requestingUrl_str = requestingUrl)
            fixed (char* topLevelUrl_str = topLevelUrl)
            {
                var n_requestingUrl = new cef_string_t(requestingUrl_str, requestingUrl != null ? requestingUrl.Length : 0);
                var n_topLevelUrl = new cef_string_t(topLevelUrl_str, topLevelUrl != null ? topLevelUrl.Length : 0);

                cef_request_context_t.set_content_setting(GetSelf(), &n_requestingUrl, &n_topLevelUrl, contentType, value);
            }
        }

    }
}
