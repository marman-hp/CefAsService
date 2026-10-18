namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefEndTracingCallback
    {
        private void on_end_tracing_complete(cef_end_tracing_callback_t* self, cef_string_t* tracing_file)
        {
            CheckSelf(self);

            OnEndTracingComplete(cef_string_t.ToString(tracing_file));
        }

        protected abstract void OnEndTracingComplete(string tracingFile);
    }
}
