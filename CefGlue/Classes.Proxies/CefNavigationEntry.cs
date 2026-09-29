namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefNavigationEntry
    {
        public bool IsValid
        {
            get { return cef_navigation_entry_t.is_valid(_self) != 0; }
        }

        public string Url
        {
            get
            {
                var n_value = cef_navigation_entry_t.get_url(_self);
                return cef_string_userfree.ToString(n_value);
            }
        }

        public string DisplayUrl
        {
            get
            {
                var n_value = cef_navigation_entry_t.get_display_url(_self);
                return cef_string_userfree.ToString(n_value);
            }
        }

        public string OriginalUrl
        {
            get
            {
                var n_value = cef_navigation_entry_t.get_original_url(_self);
                return cef_string_userfree.ToString(n_value);
            }
        }

        public string Title
        {
            get
            {
                var n_value = cef_navigation_entry_t.get_title(_self);
                return cef_string_userfree.ToString(n_value);
            }
        }

        public CefTransitionType TransitionType
        {
            get { return cef_navigation_entry_t.get_transition_type(_self); }
        }

        public bool HasPostData
        {
            get { return cef_navigation_entry_t.has_post_data(_self) != 0; }
        }

        public CefBaseTime CompletionTime
        {
            get
            {
                return cef_navigation_entry_t.get_completion_time(_self);
            }
        }

        public int HttpStatusCode
        {
            get { return cef_navigation_entry_t.get_http_status_code(_self); }
        }

        public CefSslStatus GetSslStatus()
        {
            return CefSslStatus.FromNative(
                cef_navigation_entry_t.get_sslstatus(_self)
                );
        }
    }
}
