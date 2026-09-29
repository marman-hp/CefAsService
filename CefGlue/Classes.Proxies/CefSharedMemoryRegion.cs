namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefSharedMemoryRegion
    {
        public bool IsValid
        {
            get => cef_shared_memory_region_t.is_valid(_self) != 0;
        }

        public nuint Size
        {
            get => cef_shared_memory_region_t.size(_self);
        }

        public IntPtr Memory()
        {
            return (IntPtr)cef_shared_memory_region_t.memory(_self);
        }

    }
}
