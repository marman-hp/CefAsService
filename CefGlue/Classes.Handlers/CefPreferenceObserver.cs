namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public unsafe partial class CefPreferenceObserver
    {
        private void on_preference_changed(cef_preference_observer_t* self, cef_string_t* name)
        {
            CheckSelf(self);
            OnPreferenceChanged(cef_string_t.ToString(name));
        }

        internal virtual void OnPreferenceChanged(string name)
        {
        }
    }
}
