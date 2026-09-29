namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefResourceBundle
    {
        public static CefResourceBundle GetGlobal()
        {
            return CefResourceBundle.FromNative(cef_resource_bundle_t.get_global());
        }

        public string GetLocalizedString(int stringId)
        {
            var n_result = cef_resource_bundle_t.get_localized_string(_self, stringId);
            return cef_string_userfree.ToString(n_result);
        }

        public CefBinaryValue GetDataResource(int resource_id)
        {
            return CefBinaryValue.FromNativeOrNull(
                cef_resource_bundle_t.get_data_resource(_self, resource_id)
                );
        }

        public CefBinaryValue GetDataResourceForScale(int resource_id, CefScaleFactor scale_factor)
        {
            return CefBinaryValue.FromNativeOrNull(
                cef_resource_bundle_t.get_data_resource_for_scale(_self, resource_id, scale_factor)
                );
        }
    }
}
