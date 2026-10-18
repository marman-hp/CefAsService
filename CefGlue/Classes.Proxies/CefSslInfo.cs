namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefSslInfo
    {
        public CefCertStatus CertStatus
        {
            get { return cef_sslinfo_t.get_cert_status(_self); }
        }

        public CefX509Certificate GetX509Certificate()
        {
            return CefX509Certificate.FromNative(
                cef_sslinfo_t.get_x509_certificate(_self)
                );
        }
    }
}
