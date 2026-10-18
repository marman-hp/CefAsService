namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefValue
    {
        public static CefValue Create()
        {
            return CefValue.FromNative(cef_value_t.create());
        }

        public bool IsValid
        {
            get
            {
                return cef_value_t.is_valid(_self) != 0;
            }
        }

        public bool IsOwned
        {
            get
            {
                return cef_value_t.is_owned(_self) != 0;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return cef_value_t.is_read_only(_self) != 0;
            }
        }

        public bool IsSame(CefValue that)
        {
            return cef_value_t.is_same(_self, that.ToNative()) != 0;
        }

        public bool IsEqual(CefValue that)
        {
            return cef_value_t.is_equal(_self, that.ToNative()) != 0;
        }

        public CefValue Copy()
        {
            return CefValue.FromNative(
                cef_value_t.copy(_self)
                );
        }

        public CefValueType GetValueType()
        {
            return cef_value_t.get_type(_self);
        }

        public bool GetBool()
        {
            return cef_value_t.get_bool(_self) != 0;
        }

        public int GetInt()
        {
            return cef_value_t.get_int(_self);
        }

        public double GetDouble()
        {
            return cef_value_t.get_double(_self);
        }

        public string GetString()
        {
            var n_result = cef_value_t.get_string(_self);
            return cef_string_userfree.ToString(n_result);
        }

        public CefBinaryValue GetBinary()
        {
            return CefBinaryValue.FromNative(
                cef_value_t.get_binary(_self)
                );
        }

        public CefDictionaryValue GetDictionary()
        {
            return CefDictionaryValue.FromNative(
                cef_value_t.get_dictionary(_self)
                );
        }

        public CefListValue GetList()
        {
            return CefListValue.FromNative(
                cef_value_t.get_list(_self)
                );
        }

        public bool SetNull()
        {
            return cef_value_t.set_null(_self) != 0;
        }

        public bool SetBool(bool value)
        {
            return cef_value_t.set_bool(_self, value ? 1 : 0) != 0;
        }

        public bool SetInt(int value)
        {
            return cef_value_t.set_int(_self, value) != 0;
        }

        public bool SetDouble(double value)
        {
            return cef_value_t.set_double(_self, value) != 0;
        }

        public bool SetString(string value)
        {
            fixed (char* value_str = value)
            {
                var n_value = new cef_string_t(value_str, value != null ? value.Length : 0);

                return cef_value_t.set_string(_self, &n_value) != 0;
            }
        }

        public bool SetBinary(CefBinaryValue value)
        {
            return cef_value_t.set_binary(_self, value.ToNative()) != 0;
        }

        public bool SetDictionary(CefDictionaryValue value)
        {
            return cef_value_t.set_dictionary(_self, value.ToNative()) != 0;
        }

        public bool SetList(CefListValue value)
        {
            return cef_value_t.set_list(_self, value.ToNative()) != 0;
        }
    }
}
