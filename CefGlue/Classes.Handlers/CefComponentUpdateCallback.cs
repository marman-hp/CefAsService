namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefComponentUpdateCallback
    {
        private void on_complete(cef_component_update_callback_t* self,
                                 cef_string_t* component_id,
                                 CefComponentUpdateError error)
        {
            CheckSelf(self);

            var m_componentId = cef_string_t.ToString(component_id);

            OnComplete(m_componentId, error);
        }

        protected abstract void OnComplete(string componentId, CefComponentUpdateError error);
    }
}
