namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefPreferenceRegistrar
    {
        internal void ReleaseObject()
        {
            _self = null;
        }

        public bool AddPreference(string name, CefValue defaultValue)
        {
            fixed (char* name_str = name)
            {
                var n_name = new cef_string_t(name_str, name != null ? name.Length : 0);
                return cef_preference_registrar_t.add_preference(_self,
                    &n_name,
                    defaultValue.ToNative()) != 0;
            }
        }
    }
}
