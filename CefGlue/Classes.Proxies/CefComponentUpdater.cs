namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefComponentUpdater
    {
        public static CefComponentUpdater GetGlobal()
        {
            return FromNativeOrNull(cef_component_updater_t.get());
        }

        public int ComponentCount => checked((int)cef_component_updater_t.get_component_count(_self));

        public CefComponent[] GetComponents()
        {
            var count = ComponentCount;
            if (count == 0) return new CefComponent[0];

            var ptrs = new IntPtr[count];
            var n_count = (UIntPtr)count;
            fixed (IntPtr* ptrs_ptr = ptrs)
            {
                cef_component_updater_t.get_components(_self, &n_count, (cef_component_t**)ptrs_ptr);
            }

            var result = new CefComponent[(int)n_count];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = CefComponent.FromNative((cef_component_t*)ptrs[i]);
            }
            return result;
        }

        public CefComponent GetComponentById(string componentId)
        {
            fixed (char* componentId_str = componentId)
            {
                var n_componentId = new cef_string_t(componentId_str, componentId != null ? componentId.Length : 0);
                return CefComponent.FromNativeOrNull(cef_component_updater_t.get_component_by_id(_self, &n_componentId));
            }
        }

        public void Update(string componentId, CefComponentUpdatePriority priority, CefComponentUpdateCallback callback)
        {
            fixed (char* componentId_str = componentId)
            {
                var n_componentId = new cef_string_t(componentId_str, componentId != null ? componentId.Length : 0);
                cef_component_updater_t.update(_self, &n_componentId, priority,
                    callback != null ? callback.ToNative() : null);
            }
        }
    }
}
