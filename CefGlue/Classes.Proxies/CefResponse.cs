namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefResponse
    {
        public static CefResponse Create()
        {
            return CefResponse.FromNative(
                cef_response_t.create()
                );
        }

        public bool IsReadOnly
        {
            get { return cef_response_t.is_read_only(_self) != 0; }
        }

        public CefErrorCode Error
        {
            get { return cef_response_t.get_error(_self); }
            set { cef_response_t.set_error(_self, value); }
        }

        public int Status
        {
            get { return cef_response_t.get_status(_self); }
            set { cef_response_t.set_status(_self, value); }
        }

        public string StatusText
        {
            get
            {
                var n_result = cef_response_t.get_status_text(_self);
                return cef_string_userfree.ToString(n_result);
            }
            set
            {
                fixed (char* value_str = value)
                {
                    var n_value = new cef_string_t(value_str, value != null ? value.Length : 0);
                    cef_response_t.set_status_text(_self, &n_value);
                }
            }
        }

        public string MimeType
        {
            get
            {
                var n_result = cef_response_t.get_mime_type(_self);
                return cef_string_userfree.ToString(n_result);
            }
            set
            {
                fixed (char* value_str = value)
                {
                    var n_value = new cef_string_t(value_str, value != null ? value.Length : 0);
                    cef_response_t.set_mime_type(_self, &n_value);
                }
            }
        }

        public string Charset
        {
            get
            {
                var n_result = cef_response_t.get_charset(_self);
                return cef_string_userfree.ToString(n_result);
            }
            set
            {
                fixed (char* value_str = value)
                {
                    var n_value = new cef_string_t(value_str, value != null ? value.Length : 0);
                    cef_response_t.set_charset(_self, &n_value);
                }
            }
        }

        public string GetHeaderByName(string name)
        {
            if (name == null) throw new ArgumentNullException("name");

            fixed (char* name_str = name)
            {
                var n_name = new cef_string_t(name_str, name.Length);
                var n_result = cef_response_t.get_header_by_name(_self, &n_name);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public void SetHeaderByName(string name, string value, bool overwrite)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            fixed (char* name_str = name)
            fixed (char* value_str = value)
            {
                var n_name = new cef_string_t(name_str, name.Length);
                var n_value = new cef_string_t(value_str, value != null ? value.Length : 0);
                cef_response_t.set_header_by_name(_self, &n_name, &n_value, overwrite ? 1 : 0);
            }
        }

        public NameValueCollection GetHeaderMap()
        {
            var headerMap = libcef.string_multimap_alloc();
            cef_response_t.get_header_map(_self, headerMap);
            var result = cef_string_multimap.ToNameValueCollection(headerMap);
            libcef.string_multimap_free(headerMap);
            return result;
        }

        public void SetHeaderMap(NameValueCollection headers)
        {
            var headerMap = cef_string_multimap.From(headers);
            cef_response_t.set_header_map(_self, headerMap);
            libcef.string_multimap_free(headerMap);
        }

        public string Url
        {
            get
            {
                var n_result = cef_response_t.get_url(_self);
                return cef_string_userfree.ToString(n_result);
            }
            set
            {
                fixed (char* value_str = value)
                {
                    var n_value = new cef_string_t(value_str, value != null ? value.Length : 0);
                    cef_response_t.set_url(_self, &n_value);
                }
            }
        }
    }
}
