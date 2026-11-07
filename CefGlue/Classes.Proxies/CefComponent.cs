namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefComponent
    {
        public string Id => cef_string_userfree.ToString(cef_component_t.get_id(_self));

        public string Name => cef_string_userfree.ToString(cef_component_t.get_name(_self));

        public string Version => cef_string_userfree.ToString(cef_component_t.get_version(_self));

        public CefComponentState State => cef_component_t.get_state(_self);
    }
}
