namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefV8ArrayBufferReleaseCallback
    {
        private void release_buffer(cef_v8_array_buffer_release_callback_t* self, void* buffer)
        {
            CheckSelf(self);

            ReleaseBuffer((IntPtr)buffer);
        }

        protected abstract void ReleaseBuffer(IntPtr buffer);
    }
}
