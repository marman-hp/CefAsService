namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefX509CertPrincipal
    {
        public string GetDisplayName()
        {
            return cef_string_userfree.ToString(
                cef_x509_cert_principal_t.get_display_name(_self)
                );
        }

        public string GetCommonName()
        {
            return cef_string_userfree.ToString(
                cef_x509_cert_principal_t.get_common_name(_self)
                );
        }

        public string GetLocalityName()
        {
            return cef_string_userfree.ToString(
                cef_x509_cert_principal_t.get_locality_name(_self)
                );
        }

        public string GetStateOrProvinceName()
        {
            return cef_string_userfree.ToString(
                cef_x509_cert_principal_t.get_state_or_province_name(_self)
                );
        }

        public string GetCountryName()
        {
            return cef_string_userfree.ToString(
                cef_x509_cert_principal_t.get_country_name(_self)
                );
        }

        public string[] GetOrganizationNames()
        {
            cef_string_list* n_result = libcef.string_list_alloc();
            cef_x509_cert_principal_t.get_organization_names(_self, n_result);
            var result = cef_string_list.ToArray(n_result);
            libcef.string_list_free(n_result);
            return result;
        }

        public string[] GetOrganizationUnitNames()
        {
            cef_string_list* n_result = libcef.string_list_alloc();
            cef_x509_cert_principal_t.get_organization_unit_names(_self, n_result);
            var result = cef_string_list.ToArray(n_result);
            libcef.string_list_free(n_result);
            return result;
        }
    }
}
