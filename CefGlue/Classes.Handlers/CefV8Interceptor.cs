namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefV8Interceptor
    {
        private int get_byname(cef_v8_interceptor_t* self, cef_string_t* name, cef_v8_value_t* @object, cef_v8_value_t** retval, cef_string_t* exception)
        {
            CheckSelf(self);

            var m_name = cef_string_t.ToString(name);
            var m_obj = CefV8Value.FromNative(@object);

            CefV8Value m_retval;
            string m_exception;
            if (GetByName(m_name, m_obj, out m_retval, out m_exception))
            {
                *retval = m_retval != null ? m_retval.ToNative() : null;
                cef_string_t.Copy(m_exception, exception);
                return 1;
            }
            else
            {
                return 0;
            }
        }

        protected virtual bool GetByName(string name, CefV8Value @object, out CefV8Value retval, out string exception)
        {
            retval = null;
            exception = null;
            return false;
        }

        private int get_byindex(cef_v8_interceptor_t* self, int index, cef_v8_value_t* @object, cef_v8_value_t** retval, cef_string_t* exception)
        {
            CheckSelf(self);

            var m_obj = CefV8Value.FromNative(@object);

            CefV8Value m_retval;
            string m_exception;
            if (GetByIndex(index, m_obj, out m_retval, out m_exception))
            {
                *retval = m_retval != null ? m_retval.ToNative() : null;
                cef_string_t.Copy(m_exception, exception);
                return 1;
            }
            else
            {
                return 0;
            }
        }

        protected virtual bool GetByIndex(int index, CefV8Value @object, out CefV8Value retval, out string exception)
        {
            retval = null;
            exception = null;
            return false;
        }

        private int set_byname(cef_v8_interceptor_t* self, cef_string_t* name, cef_v8_value_t* @object, cef_v8_value_t* value, cef_string_t* exception)
        {
            CheckSelf(self);

            var m_name = cef_string_t.ToString(name);
            var m_obj = CefV8Value.FromNative(@object);
            var m_value = CefV8Value.FromNative(value);

            string m_exception;
            if (SetByName(m_name, m_obj, m_value, out m_exception))
            {
                cef_string_t.Copy(m_exception, exception);
                return 1;
            }
            else
            {
                return 0;
            }
        }

        protected virtual bool SetByName(string name, CefV8Value @object, CefV8Value value, out string exception)
        {
            exception = null;
            return false;
        }

        private int set_byindex(cef_v8_interceptor_t* self, int index, cef_v8_value_t* @object, cef_v8_value_t* value, cef_string_t* exception)
        {
            CheckSelf(self);

            var m_obj = CefV8Value.FromNative(@object);
            var m_value = CefV8Value.FromNative(value);

            string m_exception;
            if (SetByIndex(index, m_obj, m_value, out m_exception))
            {
                cef_string_t.Copy(m_exception, exception);
                return 1;
            }
            else
            {
                return 0;
            }
        }

        protected virtual bool SetByIndex(int index, CefV8Value @object, CefV8Value value, out string exception)
        {
            exception = null;
            return false;
        }
    }
}
