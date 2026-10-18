namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Xml.Linq;

    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefV8Value
    {
        public static CefV8Value CreateUndefined()
        {
            return CefV8Value.FromNative(
                cef_v8_value_t.create_undefined()
                );
        }

        public static CefV8Value CreateNull()
        {
            return CefV8Value.FromNative(
                cef_v8_value_t.create_null()
                );
        }

        public static CefV8Value CreateBool(bool value)
        {
            return CefV8Value.FromNative(
                cef_v8_value_t.create_bool(value ? 1 : 0)
                );
        }

        public static CefV8Value CreateInt(int value)
        {
            return CefV8Value.FromNative(
                cef_v8_value_t.create_int(value)
                );
        }

        public static CefV8Value CreateUInt(uint value)
        {
            return CefV8Value.FromNative(
                cef_v8_value_t.create_uint(value)
                );
        }

        public static CefV8Value CreateDouble(double value)
        {
            return CefV8Value.FromNative(
                cef_v8_value_t.create_double(value)
                );
        }

        public static CefV8Value CreateDate(CefBaseTime value)
        {
            return CefV8Value.FromNative(
                cef_v8_value_t.create_date(value)
                );
        }

        public static CefV8Value CreateString(string value)
        {
            fixed (char* value_str = value)
            {
                var n_value = new cef_string_t(value_str, value != null ? value.Length : 0);
                return CefV8Value.FromNative(
                    cef_v8_value_t.create_string(&n_value)
                    );
            }
        }

        public static CefV8Value CreateObject(CefV8Accessor? accessor = null, CefV8Interceptor? interceptor = null)
        {
            return CefV8Value.FromNative(
                cef_v8_value_t.create_object(
                    accessor != null ? accessor.ToNative() : null,
                    interceptor != null ? interceptor.ToNative() : null
                    )
                );
        }

        public static CefV8Value CreateArray(int length)
        {
            return CefV8Value.FromNative(
                cef_v8_value_t.create_array(length)
                );
        }

        public static CefV8Value CreateArrayBuffer(IntPtr buffer, ulong length, CefV8ArrayBufferReleaseCallback releaseCallback)
        {
            if (releaseCallback == null) throw new ArgumentNullException(nameof(releaseCallback));

            var n_value = cef_v8_value_t.create_array_buffer(
                (void*)buffer,
                checked((UIntPtr)length),
                releaseCallback.ToNative()
                );

            return FromNative(n_value);
        }

        public static CefV8Value CreateArrayBufferWithCopy(IntPtr buffer, ulong length)
        {
            var n_value = cef_v8_value_t.create_array_buffer_with_copy((void*)buffer, checked((UIntPtr)length));
            return FromNative(n_value);
        }

        public static CefV8Value CreateFunction(string name, CefV8Handler handler)
        {
            fixed (char* name_str = name)
            {
                var n_name = new cef_string_t(name_str, name != null ? name.Length : 0);

                return CefV8Value.FromNative(
                    cef_v8_value_t.create_function(&n_name, handler.ToNative())
                    );
            }
        }

        public static CefV8Value CreatePromise()
        {
            return CefV8Value.FromNative(
                cef_v8_value_t.create_promise()
                );
        }

        public bool IsValid
        {
            get { return cef_v8_value_t.is_valid(_self) != 0; }
        }

        public bool IsUndefined
        {
            get { return cef_v8_value_t.is_undefined(_self) != 0; }
        }

        public bool IsNull
        {
            get { return cef_v8_value_t.is_null(_self) != 0; }
        }

        public bool IsBool
        {
            get { return cef_v8_value_t.is_bool(_self) != 0; }
        }

        public bool IsInt
        {
            get { return cef_v8_value_t.is_int(_self) != 0; }
        }

        public bool IsUInt
        {
            get { return cef_v8_value_t.is_uint(_self) != 0; }
        }

        public bool IsDouble
        {
            get { return cef_v8_value_t.is_double(_self) != 0; }
        }

        public bool IsDate
        {
            get { return cef_v8_value_t.is_date(_self) != 0; }
        }

        public bool IsString
        {
            get { return cef_v8_value_t.is_string(_self) != 0; }
        }

        public bool IsObject
        {
            get { return cef_v8_value_t.is_object(_self) != 0; }
        }

        public bool IsArray
        {
            get { return cef_v8_value_t.is_array(_self) != 0; }
        }

        public bool IsArrayBuffer
        {
            get { return cef_v8_value_t.is_array_buffer(_self) != 0; }
        }

        public bool IsFunction
        {
            get { return cef_v8_value_t.is_function(_self) != 0; }
        }

        public bool IsPromise
            => cef_v8_value_t.is_promise(_self) != 0;

        public bool IsSame(CefV8Value that)
        {
            if (that == null) return false;

            return cef_v8_value_t.is_same(_self, that.ToNative()) != 0;
        }

        public bool GetBoolValue()
        {
            return cef_v8_value_t.get_bool_value(_self) != 0;
        }

        public int GetIntValue()
        {
            return cef_v8_value_t.get_int_value(_self);
        }

        public uint GetUIntValue()
        {
            return cef_v8_value_t.get_uint_value(_self);
        }

        public double GetDoubleValue()
        {
            return cef_v8_value_t.get_double_value(_self);
        }

        public CefBaseTime GetDateValue()
        {
            return cef_v8_value_t.get_date_value(_self);
        }

        public string GetStringValue()
        {
            var n_result = cef_v8_value_t.get_string_value(_self);
            return cef_string_userfree.ToString(n_result);
        }

        public bool IsUserCreated
        {
            get { return cef_v8_value_t.is_user_created(_self) != 0; }
        }

        public bool HasException
        {
            get { return cef_v8_value_t.has_exception(_self) != 0; }
        }

        public CefV8Exception GetException()
        {
            return CefV8Exception.FromNativeOrNull(
                cef_v8_value_t.get_exception(_self)
                );
        }

        public bool ClearException()
        {
            return cef_v8_value_t.clear_exception(_self) != 0;
        }

        public bool WillRethrowExceptions()
        {
            return cef_v8_value_t.will_rethrow_exceptions(_self) != 0;
        }

        public bool SetRethrowExceptions(bool rethrow)
        {
            return cef_v8_value_t.set_rethrow_exceptions(_self, rethrow ? 1 : 0) != 0;
        }

        public bool HasValue(string key)
        {
            fixed (char* key_str = key)
            {
                var n_key = new cef_string_t(key_str, key != null ? key.Length : 0);
                return cef_v8_value_t.has_value_bykey(_self, &n_key) != 0;
            }
        }

        public bool HasValue(int index)
        {
            return cef_v8_value_t.has_value_byindex(_self, index) != 0;
        }

        public bool DeleteValue(string key)
        {
            fixed (char* key_str = key)
            {
                var n_key = new cef_string_t(key_str, key != null ? key.Length : 0);
                return cef_v8_value_t.delete_value_bykey(_self, &n_key) != 0;
            }
        }

        public bool DeleteValue(int index)
        {
            return cef_v8_value_t.delete_value_byindex(_self, index) != 0;
        }

        public CefV8Value GetValue(string key)
        {
            fixed (char* key_str = key)
            {
                var n_key = new cef_string_t(key_str, key != null ? key.Length : 0);
                return CefV8Value.FromNativeOrNull(
                    cef_v8_value_t.get_value_bykey(_self, &n_key)
                    );
            }
        }

        public CefV8Value GetValue(int index)
        {
            return CefV8Value.FromNativeOrNull(
                    cef_v8_value_t.get_value_byindex(_self, index)
                    );
        }

        public bool SetValue(string key, CefV8Value value, CefV8PropertyAttribute attribute = CefV8PropertyAttribute.None)
        {
            fixed (char* key_str = key)
            {
                var n_key = new cef_string_t(key_str, key != null ? key.Length : 0);
                return cef_v8_value_t.set_value_bykey(_self, &n_key, value.ToNative(), attribute) != 0;
            }
        }

        public bool SetValue(int index, CefV8Value value)
        {
            return cef_v8_value_t.set_value_byindex(_self, index, value.ToNative()) != 0;
        }

        public bool SetValue(string key, CefV8PropertyAttribute attribute = CefV8PropertyAttribute.None)
        {
            fixed (char* key_str = key)
            {
                var n_key = new cef_string_t(key_str, key != null ? key.Length : 0);
                return cef_v8_value_t.set_value_byaccessor(_self, &n_key, attribute) != 0;
            }
        }

        public bool TryGetKeys(out string[] keys)
        {
            var list = libcef.string_list_alloc();
            var result = cef_v8_value_t.get_keys(_self, list) != 0;
            if (result) keys = cef_string_list.ToArray(list);
            else keys = null;
            libcef.string_list_free(list);
            return result;
        }

        public string[] GetKeys()
        {
            string[] keys;
            if (TryGetKeys(out keys)) return keys;
            else throw new InvalidOperationException();
        }

        public bool SetUserData(CefUserData userData)
        {
            return cef_v8_value_t.set_user_data(_self, userData != null ? (cef_base_ref_counted_t*)userData.ToNative() : null) != 0;
        }

        public CefUserData GetUserData()
        {
            return CefUserData.FromNativeOrNull(
                (cef_user_data_t*)cef_v8_value_t.get_user_data(_self)
                );
        }

        public int GetExternallyAllocatedMemory()
        {
            return cef_v8_value_t.get_externally_allocated_memory(_self);
        }

        public int AdjustExternallyAllocatedMemory(int changeInBytes)
        {
            return cef_v8_value_t.adjust_externally_allocated_memory(_self, changeInBytes);
        }

        public int GetArrayLength()
        {
            return cef_v8_value_t.get_array_length(_self);
        }

        public CefV8ArrayBufferReleaseCallback GetArrayBufferReleaseCallback()
        {
            var n_releaseCallback = cef_v8_value_t.get_array_buffer_release_callback(_self);
            return CefV8ArrayBufferReleaseCallback.FromNativeOrNull(n_releaseCallback);
        }

        public bool NeuterArrayBuffer()
        {
            return cef_v8_value_t.neuter_array_buffer(_self) != 0;
        }

        public UIntPtr GetArrayBufferByteLength()
        {
            return cef_v8_value_t.get_array_buffer_byte_length(_self);
        }

        public IntPtr GetArrayBufferData()
        {
            return (IntPtr)cef_v8_value_t.get_array_buffer_data(_self);
        }

        public string GetFunctionName()
        {
            var n_result = cef_v8_value_t.get_function_name(_self);
            return cef_string_userfree.ToString(n_result);
        }

        public CefV8Handler GetFunctionHandler()
        {
            return CefV8Handler.FromNativeOrNull(
                cef_v8_value_t.get_function_handler(_self)
                );
        }

        public CefV8Value ExecuteFunction(CefV8Value obj, CefV8Value[] arguments)
        {
            var n_arguments = CreateArguments(arguments);
            cef_v8_value_t* n_retval;

            fixed (cef_v8_value_t** n_arguments_ptr = n_arguments)
            {
                n_retval = cef_v8_value_t.execute_function(
                    _self,
                    obj != null ? obj.ToNative() : null,
                    n_arguments != null ? (UIntPtr)n_arguments.Length : UIntPtr.Zero,
                    n_arguments_ptr
                    );
            }

            return CefV8Value.FromNativeOrNull(n_retval);
        }

        public CefV8Value ExecuteFunctionWithContext(CefV8Context context, CefV8Value obj, CefV8Value[] arguments)
        {
            var n_arguments = CreateArguments(arguments);
            cef_v8_value_t* n_retval;

            fixed (cef_v8_value_t** n_arguments_ptr = n_arguments)
            {
                n_retval = cef_v8_value_t.execute_function_with_context(
                    _self,
                    context.ToNative(),
                    obj != null ? obj.ToNative() : null,
                    n_arguments != null ? (UIntPtr)n_arguments.Length : UIntPtr.Zero,
                    n_arguments_ptr
                    );
            }

            return CefV8Value.FromNativeOrNull(n_retval);
        }

        private static cef_v8_value_t*[] CreateArguments(CefV8Value[] arguments)
        {
            if (arguments == null) return null;

            var length = arguments.Length;
            if (length == 0) return null;

            var result = new cef_v8_value_t*[arguments.Length];

            for (var i = 0; i < length; i++)
            {
                result[i] = arguments[i].ToNative();
            }

            return result;
        }

        public bool ResolvePromise(CefV8Value value)
        {
            return cef_v8_value_t.resolve_promise(_self, value.ToNative()) != 0;
        }

        public bool RejectPromise(string errorMessage)
        {
            fixed (char* errorMessage_str = errorMessage)
            {
                var n_errorMessage = new cef_string_t(errorMessage_str, errorMessage != null ? errorMessage.Length : 0);
                return cef_v8_value_t.reject_promise(_self, &n_errorMessage) != 0;
            }
        }
    }
}
