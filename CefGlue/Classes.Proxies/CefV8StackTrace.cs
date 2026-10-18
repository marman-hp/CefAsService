namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefV8StackTrace
    {
        public static CefV8StackTrace GetCurrent(int frameLimit)
        {
            return CefV8StackTrace.FromNative(
                cef_v8_stack_trace_t.get_current(frameLimit)
                );
        }

        public bool IsValid
        {
            get { return cef_v8_stack_trace_t.is_valid(_self) != 0; }
        }

        public int FrameCount
        {
            get { return cef_v8_stack_trace_t.get_frame_count(_self); }
        }

        public CefV8StackFrame GetFrame(int index)
        {
            return CefV8StackFrame.FromNative(
                cef_v8_stack_trace_t.get_frame(_self, index)
                );
        }
    }
}
