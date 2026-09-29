namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefX509Certificate
    {
        public CefX509CertPrincipal GetSubject()
        {
            return CefX509CertPrincipal.FromNative(
                cef_x509_certificate_t.get_subject(_self)
                );
        }

        public CefX509CertPrincipal GetIssuer()
        {
            return CefX509CertPrincipal.FromNative(
                cef_x509_certificate_t.get_issuer(_self)
                );
        }

        public CefBinaryValue GetSerialNumber()
        {
            var n_result = cef_x509_certificate_t.get_serial_number(_self);
            return CefBinaryValue.FromNative(n_result);
        }

        public CefBaseTime GetValidStart()
        {
            return cef_x509_certificate_t.get_valid_start(_self);
        }

        public CefBaseTime GetValidExpiry()
        {
            return cef_x509_certificate_t.get_valid_expiry(_self);
        }

        public CefBinaryValue GetDerEncoded()
        {
            var n_result = cef_x509_certificate_t.get_derencoded(_self);
            return CefBinaryValue.FromNative(n_result);
        }

        public CefBinaryValue GetPemEncoded()
        {
            var n_result = cef_x509_certificate_t.get_pemencoded(_self);
            return CefBinaryValue.FromNative(n_result);
        }

        public long GetIssuerChainSize()
        {
            return (long)cef_x509_certificate_t.get_issuer_chain_size(_self);
        }

        public void GetDerEncodedIssuerChain(out long chainCount, out CefBinaryValue chain)
        {
            UIntPtr n_chainCount;
            cef_binary_value_t* n_chain;

            cef_x509_certificate_t.get_derencoded_issuer_chain(_self, &n_chainCount, &n_chain);

            chainCount = (long)n_chainCount;
            chain = CefBinaryValue.FromNative(n_chain);
        }

        public void GetPEMEncodedIssuerChain(out long chainCount, out CefBinaryValue chain)
        {
            UIntPtr n_chainCount;
            cef_binary_value_t* n_chain;

            cef_x509_certificate_t.get_pemencoded_issuer_chain(_self, &n_chainCount, &n_chain);

            chainCount = (long)n_chainCount;
            chain = CefBinaryValue.FromNative(n_chain);
        }
    }
}
