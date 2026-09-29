namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefListValue : ICefListValue
    {
        public static CefListValue Create()
        {
            return CefListValue.FromNative(
                cef_list_value_t.create()
                );
        }

        public bool IsValid
        {
            get { return cef_list_value_t.is_valid(_self) != 0; }
        }

        public bool IsOwned
        {
            get { return cef_list_value_t.is_owned(_self) != 0; }
        }

        public bool IsReadOnly
        {
            get { return cef_list_value_t.is_read_only(_self) != 0; }
        }

        public bool IsSame(ICefListValue that)
        {
            return cef_list_value_t.is_same(_self, ((CefListValue)that).ToNative()) != 0;
        }

        public bool IsEqual(ICefListValue that)
        {
            return cef_list_value_t.is_equal(_self, ((CefListValue)that).ToNative()) != 0;
        }

        public ICefListValue Copy()
        {
            return CefListValue.FromNative(
                cef_list_value_t.copy(_self)
                );
        }

        public bool SetSize(int size)
        {
            return cef_list_value_t.set_size(_self, (UIntPtr)size) != 0;
        }

        public int Count
        {
            get { return (int)cef_list_value_t.get_size(_self); }
        }

        public bool Clear()
        {
            return cef_list_value_t.clear(_self) != 0;
        }

        public bool Remove(int index)
        {
            return cef_list_value_t.remove(_self, checked((UIntPtr)index)) != 0;
        }

        public CefValueType GetValueType(int index)
        {
            return cef_list_value_t.get_type(_self, checked((UIntPtr)index));
        }

        public CefValue GetValue(int index)
        {
            return CefValue.FromNativeOrNull(
                cef_list_value_t.get_value(_self, checked((UIntPtr)index))
                );
        }

        public bool GetBool(int index)
        {
            return cef_list_value_t.get_bool(_self, checked((UIntPtr)index)) != 0;
        }

        public int GetInt(int index)
        {
            return cef_list_value_t.get_int(_self, checked((UIntPtr)index));
        }

        public double GetDouble(int index)
        {
            return cef_list_value_t.get_double(_self, checked((UIntPtr)index));
        }

        public string GetString(int index)
        {
            var n_result = cef_list_value_t.get_string(_self, checked((UIntPtr)index));
            return cef_string_userfree.ToString(n_result);
        }

        public ICefBinaryValue GetBinary(int index)
        {
            return CefBinaryValue.FromNativeOrNull(
                cef_list_value_t.get_binary(_self, checked((UIntPtr)index))
                );
        }

        public ICefDictionaryValue GetDictionary(int index)
        {
            return CefDictionaryValue.FromNativeOrNull(
                cef_list_value_t.get_dictionary(_self, checked((UIntPtr)index))
                );
        }

        public ICefListValue GetList(int index)
        {
            return CefListValue.FromNativeOrNull(
                cef_list_value_t.get_list(_self, checked((UIntPtr)index))
                );
        }

        public bool SetValue(int index, CefValue value)
        {
            return cef_list_value_t.set_value(_self, checked((UIntPtr)index), value.ToNative()) != 0;
        }

        public bool SetNull(int index)
        {
            return cef_list_value_t.set_null(_self, checked((UIntPtr)index)) != 0;
        }

        public bool SetBool(int index, bool value)
        {
            return cef_list_value_t.set_bool(_self, checked((UIntPtr)index), value ? 1 : 0) != 0;
        }

        public bool SetInt(int index, int value)
        {
            return cef_list_value_t.set_int(_self, checked((UIntPtr)index), value) != 0;
        }

        public bool SetDouble(int index, double value)
        {
            return cef_list_value_t.set_double(_self, checked((UIntPtr)index), value) != 0;
        }

        public bool SetString(int index, string value)
        {
            fixed (char* value_str = value)
            {
                var n_value = new cef_string_t(value_str, value != null ? value.Length : 0);
                return cef_list_value_t.set_string(_self, checked((UIntPtr)index), &n_value) != 0;
            }
        }

        public bool SetBinary(int index, ICefBinaryValue value)
        {
            return cef_list_value_t.set_binary(_self, checked((UIntPtr)index), ((CefBinaryValue)value).ToNative()) != 0;
        }

        public bool SetDictionary(int index, ICefDictionaryValue value)
        {
            return cef_list_value_t.set_dictionary(_self, checked((UIntPtr)index), ((CefDictionaryValue)value).ToNative()) != 0;
        }

        public bool SetList(int index, ICefListValue value)
        {
            return cef_list_value_t.set_list(_self, checked((UIntPtr)index), ((CefListValue)value).ToNative()) != 0;
        }
    }
}
