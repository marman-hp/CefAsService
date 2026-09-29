namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefSslStatus
    {
        public bool IsSecureConnection
        {
            get { return cef_sslstatus_t.is_secure_connection(_self) != 0; }
        }

        public CefCertStatus CertStatus
        {
            get { return cef_sslstatus_t.get_cert_status(_self); }
        }

        public CefSslVersion SslVersion
        {
            get { return cef_sslstatus_t.get_sslversion(_self); }
        }

        public CefSslContentStatus ContentStatus
        {
            get { return cef_sslstatus_t.get_content_status(_self); }
        }

        public CefX509Certificate GetX509Certificate()
        {
            return CefX509Certificate.FromNative(
                cef_sslstatus_t.get_x509_certificate(_self)
                );
        }
    }
}
