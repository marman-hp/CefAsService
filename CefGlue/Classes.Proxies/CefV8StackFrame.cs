namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefV8StackFrame
    {
        public bool IsValid
        {
            get { return cef_v8_stack_frame_t.is_valid(_self) != 0; }
        }

        public string ScriptName
        {
            get
            {
                var n_result = cef_v8_stack_frame_t.get_script_name(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string ScriptNameOrSourceUrl
        {
            get
            {
                var n_result = cef_v8_stack_frame_t.get_script_name_or_source_url(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string FunctionName
        {
            get
            {
                var n_result = cef_v8_stack_frame_t.get_function_name(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public int LineNumber
        {
            get { return cef_v8_stack_frame_t.get_line_number(_self); }
        }

        public int Column
        {
            get { return cef_v8_stack_frame_t.get_column(_self); }
        }

        public bool IsEval
        {
            get { return cef_v8_stack_frame_t.is_eval(_self) != 0; }
        }

        public bool IsConstructor
        {
            get { return cef_v8_stack_frame_t.is_constructor(_self) != 0; }
        }
    }
}
