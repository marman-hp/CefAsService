namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefRequest
    {
        public static CefRequest Create()
        {
            return CefRequest.FromNative(
                cef_request_t.create()
                );
        }

        public bool IsReadOnly
        {
            get { return cef_request_t.is_read_only(_self) != 0; }
        }

        public string Url
        {
            get
            {
                var n_result = cef_request_t.get_url(_self);
                return cef_string_userfree.ToString(n_result);
            }
            set
            {
                if (value == null) throw new ArgumentNullException("value");

                fixed (char* value_str = value)
                {
                    var n_value = new cef_string_t(value_str, value.Length);
                    cef_request_t.set_url(_self, &n_value);
                }
            }
        }

        public string Method
        {
            get
            {
                var n_result = cef_request_t.get_method(_self);
                return cef_string_userfree.ToString(n_result);
            }
            set
            {
                fixed (char* value_str = value)
                {
                    var n_value = new cef_string_t(value_str, value != null ? value.Length : 0);
                    cef_request_t.set_method(_self, &n_value);
                }
            }
        }

        public void SetReferrer(string referrerUrl, CefReferrerPolicy policy)
        {
            fixed (char* referrerUrl_str = referrerUrl)
            {
                var n_referrerUrl = new cef_string_t(referrerUrl_str, referrerUrl != null ? referrerUrl.Length : 0);
                cef_request_t.set_referrer(_self, &n_referrerUrl, policy);
            }
        }

        public string ReferrerURL
        {
            get
            {
                var n_result = cef_request_t.get_referrer_url(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public CefReferrerPolicy ReferrerPolicy
        {
            get
            {
                return cef_request_t.get_referrer_policy(_self);
            }
        }

        public CefPostData PostData
        {
            get
            {
                return CefPostData.FromNativeOrNull(
                    cef_request_t.get_post_data(_self)
                    );
            }
            set
            {
                var n_value = value != null ? value.ToNative() : null;
                cef_request_t.set_post_data(_self, n_value);
            }
        }

        public NameValueCollection GetHeaderMap()
        {
            var headerMap = libcef.string_multimap_alloc();
            cef_request_t.get_header_map(_self, headerMap);
            var result = cef_string_multimap.ToNameValueCollection(headerMap);
            libcef.string_multimap_free(headerMap);
            return result;
        }

        public void SetHeaderMap(NameValueCollection headers)
        {
            var headerMap = cef_string_multimap.From(headers);
            cef_request_t.set_header_map(_self, headerMap);
            libcef.string_multimap_free(headerMap);
        }

        public string GetHeaderByName(string name)
        {
            fixed (char* name_str = name)
            {
                var n_name = new cef_string_t(name_str, name != null ? name.Length : 0);
                var n_result = cef_request_t.get_header_by_name(_self, &n_name);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public void SetHeaderByName(string name, string value, bool overwrite)
        {
            fixed (char* name_str = name)
            fixed (char* value_str = value)
            {
                var n_name = new cef_string_t(name_str, name != null ? name.Length : 0);
                var n_value = new cef_string_t(value_str, value != null ? value.Length : 0);
                cef_request_t.set_header_by_name(_self, &n_name, &n_value, overwrite ? 1 : 0);
            }
        }

        public void Set(string url, string method, CefPostData postData, NameValueCollection headers)
        {
            fixed (char* url_str = url)
            fixed (char* method_str = method)
            {
                var n_url = new cef_string_t(url_str, url != null ? url.Length : 0);
                var n_method = new cef_string_t(method_str, method_str != null ? method.Length : 0);
                var n_postData = postData != null ? postData.ToNative() : null;
                var n_headerMap = cef_string_multimap.From(headers);
                cef_request_t.set(_self, &n_url, &n_method, n_postData, n_headerMap);
                libcef.string_multimap_free(n_headerMap);
            }
        }

        public CefUrlRequestOptions Options
        {
            get { return (CefUrlRequestOptions)cef_request_t.get_flags(_self); }
            set { cef_request_t.set_flags(_self, (int)value); }
        }

        public string FirstPartyForCookies
        {
            get
            {
                var n_result = cef_request_t.get_first_party_for_cookies(_self);
                return cef_string_userfree.ToString(n_result);
            }
            set
            {
                fixed (char* value_str = value)
                {
                    var n_value = new cef_string_t(value_str, value != null ? value.Length : 0);
                    cef_request_t.set_first_party_for_cookies(_self, &n_value);
                }
            }
        }

        public CefResourceType ResourceType
        {
            get
            {
                return cef_request_t.get_resource_type(_self);
            }
        }

        public CefTransitionType TransitionType
        {
            get
            {
                return cef_request_t.get_transition_type(_self);
            }
        }

        public ulong Identifier
        {
            get
            {
                return cef_request_t.get_identifier(_self);
            }
        }
    }
}
