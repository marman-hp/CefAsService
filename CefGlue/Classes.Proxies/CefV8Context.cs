namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefV8Context
    {
        public static CefV8Context GetCurrentContext()
        {
            return CefV8Context.FromNative(
                cef_v8_context_t.get_current_context()
                );
        }

        public static CefV8Context GetEnteredContext()
        {
            return CefV8Context.FromNative(
                cef_v8_context_t.get_entered_context()
                );
        }

        public static bool InContext
        {
            get { return cef_v8_context_t.in_context() != 0; }
        }

        public CefTaskRunner GetTaskRunner()
        {
            return CefTaskRunner.FromNative(
                cef_v8_context_t.get_task_runner(_self)
                );
        }

        public bool IsValid
        {
            get { return cef_v8_context_t.is_valid(_self) != 0; }
        }

        public CefBrowser GetBrowser()
        {
            return CefBrowser.FromNativeOrNull(
                cef_v8_context_t.get_browser(_self)
                );
        }

        public CefFrame GetFrame()
        {
            return CefFrame.FromNativeOrNull(
                cef_v8_context_t.get_frame(_self)
                );
        }

        public CefV8Value GetGlobal()
        {
            return CefV8Value.FromNative(
                cef_v8_context_t.get_global(_self)
                );
        }

        public bool Enter()
        {
            return cef_v8_context_t.enter(_self) != 0;
        }

        public bool Exit()
        {
            return cef_v8_context_t.exit(_self) != 0;
        }

        public bool IsSame(CefV8Context that)
        {
            if (that == null) return false;
            return cef_v8_context_t.is_same(_self, that.ToNative()) != 0;
        }

        public bool TryEval(string code, string scriptUrl, int startLine,
            out CefV8Value? returnValue, out CefV8Exception? exception)
        {
            bool result;
            cef_v8_value_t* n_retval = null;
            cef_v8_exception_t* n_exception = null;

            fixed (char* code_str = code)
            fixed (char* scriptUrl_str = scriptUrl)
            {
                var n_code = new cef_string_t(code_str, code != null ? code.Length : 0);
                var n_scriptUrl = new cef_string_t(scriptUrl_str, scriptUrl != null ? scriptUrl.Length : 0);
                result = cef_v8_context_t.eval(_self, &n_code, &n_scriptUrl, startLine, &n_retval, &n_exception) != 0;
            }

            returnValue = CefV8Value.FromNativeOrNull(n_retval);
            exception = CefV8Exception.FromNativeOrNull(n_exception);

            return result;
        }
    }
}
