namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefDictionaryValue : ICefDictionaryValue
    {
        public static CefDictionaryValue Create()
        {
            return CefDictionaryValue.FromNative(
                cef_dictionary_value_t.create()
                );
        }

        public bool IsValid
        {
            get { return cef_dictionary_value_t.is_valid(_self) != 0; }
        }

        public bool IsOwned
        {
            get { return cef_dictionary_value_t.is_owned(_self) != 0; }
        }

        public bool IsReadOnly
        {
            get { return cef_dictionary_value_t.is_read_only(_self) != 0; }
        }

        public bool IsSame(ICefDictionaryValue that)
        {
            return cef_dictionary_value_t.is_same(_self, ((CefDictionaryValue)that).ToNative()) != 0;
        }

        public bool IsEqual(ICefDictionaryValue that)
        {
            return cef_dictionary_value_t.is_equal(_self, ((CefDictionaryValue)that).ToNative()) != 0;
        }

        public ICefDictionaryValue Copy(bool excludeEmptyChildren)
        {
            return CefDictionaryValue.FromNative(
                cef_dictionary_value_t.copy(_self, excludeEmptyChildren ? 1 : 0)
                );
        }

        public int Count
        {
            get { return (int)cef_dictionary_value_t.get_size(_self); }
        }

        public bool Clear()
        {
            return cef_dictionary_value_t.clear(_self) != 0;
        }

        public bool HasKey(string key)
        {
            fixed (char* key_str = key)
            {
                var n_key = new cef_string_t(key_str, key != null ? key.Length : 0);

                return cef_dictionary_value_t.has_key(_self, &n_key) != 0;
            }
        }

        public string[] GetKeys()
        {
            var list = libcef.string_list_alloc();
            var success = cef_dictionary_value_t.get_keys(_self, list) != 0;
            if (!success) throw new InvalidOperationException();
            var result = cef_string_list.ToArray(list);
            libcef.string_list_free(list);
            return result;
        }

        public bool Remove(string key)
        {
            fixed (char* key_str = key)
            {
                var n_key = new cef_string_t(key_str, key != null ? key.Length : 0);
                return cef_dictionary_value_t.remove(_self, &n_key) != 0;
            }
        }

        public CefValueType GetValueType(string key)
        {
            fixed (char* key_str = key)
            {
                var n_key = new cef_string_t(key_str, key != null ? key.Length : 0);
                return cef_dictionary_value_t.get_type(_self, &n_key);
            }
        }

        public CefValue GetValue(string key)
        {
            fixed (char* key_str = key)
            {
                var n_key = new cef_string_t(key_str, key != null ? key.Length : 0);

                var n_result = cef_dictionary_value_t.get_value(_self, &n_key);

                return CefValue.FromNativeOrNull(n_result);
            }
        }

        public bool GetBool(string key)
        {
            fixed (char* key_str = key)
            {
                var n_key = new cef_string_t(key_str, key != null ? key.Length : 0);
                return cef_dictionary_value_t.get_bool(_self, &n_key) != 0;
            }
        }

        public int GetInt(string key)
        {
            fixed (char* key_str = key)
            {
                var n_key = new cef_string_t(key_str, key != null ? key.Length : 0);
                return cef_dictionary_value_t.get_int(_self, &n_key);
            }
        }

        public double GetDouble(string key)
        {
            fixed (char* key_str = key)
            {
                var n_key = new cef_string_t(key_str, key != null ? key.Length : 0);
                return cef_dictionary_value_t.get_double(_self, &n_key);
            }
        }

        public string GetString(string key)
        {
            fixed (char* key_str = key)
            {
                var n_key = new cef_string_t(key_str, key != null ? key.Length : 0);
                var n_result = cef_dictionary_value_t.get_string(_self, &n_key);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public ICefBinaryValue GetBinary(string key)
        {
            fixed (char* key_str = key)
            {
                var n_key = new cef_string_t(key_str, key != null ? key.Length : 0);
                var n_result = cef_dictionary_value_t.get_binary(_self, &n_key);
                return CefBinaryValue.FromNative(n_result);
            }
        }

        public ICefDictionaryValue GetDictionary(string key)
        {
            fixed (char* key_str = key)
            {
                var n_key = new cef_string_t(key_str, key != null ? key.Length : 0);
                var n_result = cef_dictionary_value_t.get_dictionary(_self, &n_key);
                return CefDictionaryValue.FromNative(n_result);
            }
        }

        public ICefListValue GetList(string key)
        {
            fixed (char* key_str = key)
            {
                var n_key = new cef_string_t(key_str, key != null ? key.Length : 0);
                var n_result = cef_dictionary_value_t.get_list(_self, &n_key);
                return CefListValue.FromNative(n_result);
            }
        }

        public bool SetValue(string key, CefValue value)
        {
            fixed (char* key_str = key)
            {
                var n_key = new cef_string_t(key_str, key != null ? key.Length : 0);
                var n_value = value.ToNative();

                return cef_dictionary_value_t.set_value(_self, &n_key, n_value) != 0;
            }
        }

        public bool SetNull(string key)
        {
            fixed (char* key_str = key)
            {
                var n_key = new cef_string_t(key_str, key != null ? key.Length : 0);
                return cef_dictionary_value_t.set_null(_self, &n_key) != 0;
            }
        }

        public bool SetBool(string key, bool value)
        {
            fixed (char* key_str = key)
            {
                var n_key = new cef_string_t(key_str, key != null ? key.Length : 0);
                return cef_dictionary_value_t.set_bool(_self, &n_key, value ? 1 : 0) != 0;
            }
        }

        public bool SetInt(string key, int value)
        {
            fixed (char* key_str = key)
            {
                var n_key = new cef_string_t(key_str, key != null ? key.Length : 0);
                return cef_dictionary_value_t.set_int(_self, &n_key, value) != 0;
            }
        }

        public bool SetDouble(string key, double value)
        {
            fixed (char* key_str = key)
            {
                var n_key = new cef_string_t(key_str, key != null ? key.Length : 0);
                return cef_dictionary_value_t.set_double(_self, &n_key, value) != 0;
            }
        }

        public bool SetString(string key, string value)
        {
            fixed (char* key_str = key)
            fixed (char* value_str = value)
            {
                var n_key = new cef_string_t(key_str, key != null ? key.Length : 0);
                var n_value = new cef_string_t(value_str, value != null ? value.Length : 0);
                return cef_dictionary_value_t.set_string(_self, &n_key, &n_value) != 0;
            }
        }

        public bool SetBinary(string key, ICefBinaryValue value)
        {
            if (value == null) throw new ArgumentNullException("value");

            fixed (char* key_str = key)
            {
                var n_key = new cef_string_t(key_str, key != null ? key.Length : 0);
                return cef_dictionary_value_t.set_binary(_self, &n_key, ((CefBinaryValue)value).ToNative()) != 0;
            }
        }

        public bool SetDictionary(string key, ICefDictionaryValue value)
        {
            if (value == null) throw new ArgumentNullException("value");

            fixed (char* key_str = key)
            {
                var n_key = new cef_string_t(key_str, key != null ? key.Length : 0);
                return cef_dictionary_value_t.set_dictionary(_self, &n_key, ((CefDictionaryValue)value).ToNative()) != 0;
            }
        }

        public bool SetList(string key, ICefListValue value)
        {
            if (value == null) throw new ArgumentNullException("value");

            fixed (char* key_str = key)
            {
                var n_key = new cef_string_t(key_str, key != null ? key.Length : 0);
                return cef_dictionary_value_t.set_list(_self, &n_key, ((CefListValue)value).ToNative()) != 0;
            }
        }
    }
}
