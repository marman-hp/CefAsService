namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefCompletionCallback
    {
        private void on_complete(cef_completion_callback_t* self)
        {
            CheckSelf(self);

            OnComplete();
        }

        protected abstract void OnComplete();
    }
}
