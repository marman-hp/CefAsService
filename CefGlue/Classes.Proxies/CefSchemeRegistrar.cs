namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefSchemeRegistrar
    {
        internal void ReleaseObject()
        {
            _self = null;
        }

        public bool AddCustomScheme(string schemeName, CefSchemeOptions options)
        {
            if (schemeName == null)
                throw new ArgumentNullException(nameof(schemeName));

            fixed (char* schemeName_str = schemeName)
            {
                var n_schemeName = new cef_string_t(schemeName_str, schemeName.Length);
                return cef_scheme_registrar_t.add_custom_scheme(
                    _self,
                    &n_schemeName,
                    (int)options
                    ) != 0;
            }
        }
    }
}
