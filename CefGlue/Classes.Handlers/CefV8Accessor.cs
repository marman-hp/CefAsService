namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefV8Accessor
    {
        private int get(cef_v8_accessor_t* self, cef_string_t* name, cef_v8_value_t* @object, cef_v8_value_t** retval, cef_string_t* exception)
        {
            CheckSelf(self);

            var m_name = cef_string_t.ToString(name);
            var m_obj = CefV8Value.FromNative(@object);
            CefV8Value m_returnValue;
            string mException;

            var handled = Get(m_name, m_obj, out m_returnValue, out mException);

            if (handled)
            {
                if (mException != null)
                {
                    cef_string_t.Copy(mException, exception);
                }
                else if (m_returnValue != null)
                {
                    *retval = m_returnValue.ToNative();
                }
            }

            return handled ? 1 : 0;
        }

        protected abstract bool Get(string name, CefV8Value obj, out CefV8Value returnValue, out string exception);

        private int set(cef_v8_accessor_t* self, cef_string_t* name, cef_v8_value_t* @object, cef_v8_value_t* value, cef_string_t* exception)
        {
            CheckSelf(self);

            var m_name = cef_string_t.ToString(name);
            var m_obj = CefV8Value.FromNative(@object);
            var m_value = CefV8Value.FromNative(value);
            string mException;

            var handled = this.Set(m_name, m_obj, m_value, out mException);

            if (handled)
            {
                if (mException != null)
                {
                    cef_string_t.Copy(mException, exception);
                }
            }

            return handled ? 1 : 0;
        }

        protected abstract bool Set(string name, CefV8Value obj, CefV8Value value, out string exception);
    }
}
