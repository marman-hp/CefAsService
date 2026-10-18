namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefV8Exception
    {
        public string Message
        {
            get
            {
                var n_result = cef_v8_exception_t.get_message(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string SourceLine
        {
            get
            {
                var n_result = cef_v8_exception_t.get_source_line(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string ScriptResourceName
        {
            get
            {
                var n_result = cef_v8_exception_t.get_script_resource_name(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public int LineNumber
        {
            get { return cef_v8_exception_t.get_line_number(_self); }
        }

        public int StartPosition
        {
            get { return cef_v8_exception_t.get_start_position(_self); }
        }

        public int EndPosition
        {
            get { return cef_v8_exception_t.get_end_position(_self); }
        }

        public int StartColumn
        {
            get { return cef_v8_exception_t.get_start_column(_self); }
        }

        public int EndColumn
        {
            get { return cef_v8_exception_t.get_end_column(_self); }
        }
    }
}
