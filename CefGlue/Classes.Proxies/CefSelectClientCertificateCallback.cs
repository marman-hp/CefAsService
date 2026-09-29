namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefSelectClientCertificateCallback
    {
        public void Select(CefX509Certificate cert)
        {
            cef_select_client_certificate_callback_t.select(_self, cert != null ? cert.ToNative() : null);
        }
    }
}
