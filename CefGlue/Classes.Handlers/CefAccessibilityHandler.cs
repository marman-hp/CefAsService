namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefAccessibilityHandler
    {
        private void on_accessibility_tree_change(cef_accessibility_handler_t* self, cef_value_t* value)
        {
            CheckSelf(self);

            using (var mValue = CefValue.FromNativeOrNull(value))
            {
                OnAccessibilityTreeChange(mValue);
            }
        }

        protected abstract void OnAccessibilityTreeChange(CefValue value);

        private void on_accessibility_location_change(cef_accessibility_handler_t* self, cef_value_t* value)
        {
            CheckSelf(self);

            using (var mValue = CefValue.FromNativeOrNull(value))
            {
                OnAccessibilityLocationChange(mValue);
            }
        }

        protected abstract void OnAccessibilityLocationChange(CefValue value);
    }
}
