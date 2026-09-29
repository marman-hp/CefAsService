namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefMediaSource
    {
        public string Id
        {
            get
            {
                var n_result = cef_media_source_t.get_id(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public bool IsCastSource => cef_media_source_t.is_cast_source(_self) != 0;

        public bool IsDialSource => cef_media_source_t.is_dial_source(_self) != 0;
    }
}
