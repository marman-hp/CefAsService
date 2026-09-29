namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public unsafe partial class CefPreferenceManager
    {
        public static CefPreferenceManager GetGlobalPreferenceManager()
        {
            var n = cef_preference_manager_t.get_global();
            return CefPreferenceManager.FromNative(n);
        }

        public bool HasPreference(string name)
        {
            fixed (char* name_str = name)
            {
                var n_name = new cef_string_t(name_str, name != null ? name.Length : 0);
                return cef_preference_manager_t.has_preference(_self, &n_name) != 0;
            }
        }

        public CefValue? GetPreference(string name)
        {
            fixed (char* name_str = name)
            {
                var n_name = new cef_string_t(name_str, name != null ? name.Length : 0);
                var n_value = cef_preference_manager_t.get_preference(_self, &n_name);
                return CefValue.FromNativeOrNull(n_value);
            }
        }

        public CefDictionaryValue GetAllPreferences(bool includeDefaults)
        {
            var n_result = cef_preference_manager_t.get_all_preferences(_self, includeDefaults ? 1 : 0);
            return CefDictionaryValue.FromNative(n_result);
        }

        public bool CanSetPreference(string name)
        {
            fixed (char* name_str = name)
            {
                var n_name = new cef_string_t(name_str, name != null ? name.Length : 0);
                return cef_preference_manager_t.can_set_preference(_self, &n_name) != 0;
            }
        }

        public bool SetPreference(string name, CefValue? value, out string error)
        {
            fixed (char* name_str = name)
            {
                var n_name = new cef_string_t(name_str, name != null ? name.Length : 0);
                var n_value = value != null ? value.ToNative() : null;
                cef_string_t n_error;

                var n_result = cef_request_context_t.set_preference(_self, &n_name, n_value, &n_error);

                error = cef_string_t.ToString(&n_error);
                return n_result != 0;
            }
        }
    }
}
