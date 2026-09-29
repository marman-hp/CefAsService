namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefCommandLine
    {
        public static CefCommandLine Create()
        {
            return CefCommandLine.FromNative(cef_command_line_t.create());
        }

        public static CefCommandLine Global
        {
            get
            {
                return CefCommandLine.FromNative(cef_command_line_t.get_global());
            }
        }

        public bool IsValid
        {
            get { return cef_command_line_t.is_valid(_self) != 0; }
        }

        public bool IsReadOnly
        {
            get { return cef_command_line_t.is_read_only(_self) != 0; }
        }

        public CefCommandLine Copy()
        {
            return CefCommandLine.FromNative(cef_command_line_t.copy(_self));
        }

        public void Reset()
        {
            cef_command_line_t.reset(_self);
        }

        public string[] GetArgv()
        {
            var list = libcef.string_list_alloc();
            cef_command_line_t.get_argv(_self, list);
            var result = cef_string_list.ToArray(list);
            libcef.string_list_free(list);
            return result;
        }

        public override string ToString()
        {
            return cef_string_userfree.ToString(
                cef_command_line_t.get_command_line_string(_self)
                );
        }

        public string GetProgram()
        {
            return cef_string_userfree.ToString(
                cef_command_line_t.get_program(_self)
                ) ?? "";
        }

        public void SetProgram(string value)
        {

            fixed (char* value_str = value)
            {
                var n_value = new cef_string_t(value_str, value != null ? value.Length : 0);

                cef_command_line_t.set_program(_self, &n_value);
            }
        }

        public bool HasSwitches
        {
            get { return cef_command_line_t.has_switches(_self) != 0; }
        }

        public bool HasSwitch(string name)
        {

            fixed (char* name_str = name)
            {
                var n_name = new cef_string_t(name_str, name.Length);

                return cef_command_line_t.has_switch(_self, &n_name) != 0;
            }
        }

        public string GetSwitchValue(string name)
        {

            fixed (char* name_str = name)
            {
                var n_name = new cef_string_t(name_str, name.Length);

                return cef_string_userfree.ToString(
                    cef_command_line_t.get_switch_value(_self, &n_name)
                    );
            }
        }

        public IDictionary<string, string> GetSwitches()
        {
            var switches = libcef.string_map_alloc();
            cef_command_line_t.get_switches(_self, switches);
            var result = cef_string_map.ToDictionary(switches);
            libcef.string_map_free(switches);
            return result;
        }

        public void AppendSwitch(string name)
        {
            if (StringHelper.IsNullOrWhiteSpace(name))
            {
                if (name == null) throw new ArgumentNullException("name");
                throw new ArgumentException("Switch name must be non empty or whitespace only string.", "name");
            }

            fixed (char* name_str = name)
            {
                var n_name = new cef_string_t(name_str, name.Length);

                cef_command_line_t.append_switch(_self, &n_name);
            }
        }

        public void AppendSwitch(string name, string value)
        {
            if (StringHelper.IsNullOrWhiteSpace(name))
            {
                if (name == null) throw new ArgumentNullException("name");
                throw new ArgumentException("Switch name must be non empty or whitespace only string.", "name");
            }

            fixed (char* name_str = name)
            fixed (char* value_str = value)
            {
                var n_name = new cef_string_t(name_str, name.Length);
                var n_value = new cef_string_t(value_str, value != null ? value.Length : 0);

                cef_command_line_t.append_switch_with_value(_self, &n_name, &n_value);
            }
        }

        public bool HasArguments
        {
            get { return cef_command_line_t.has_arguments(_self) != 0; }
        }

        public string[] GetArguments()
        {
            var arguments = libcef.string_list_alloc();
            cef_command_line_t.get_arguments(_self, arguments);
            var result = cef_string_list.ToArray(arguments);
            libcef.string_list_free(arguments);
            return result;
        }

        public void AppendArgument(string value)
        {
            fixed (char* value_str = value)
            {
                var n_value = new cef_string_t(value_str, value.Length);

                cef_command_line_t.append_argument(_self, &n_value);
            }
        }

        public void PrependWrapper(string wrapper)
        {
            fixed (char* wrapper_str = wrapper)
            {
                var n_wrapper = new cef_string_t(wrapper_str, wrapper.Length);

                cef_command_line_t.prepend_wrapper(_self, &n_wrapper);
            }
        }

        public void PrependArgument(string argument)
        {
            if (argument.IndexOf(' ') >= 0)
            {
                PrependWrapper(".");
                SetProgram(argument);
            }
            else PrependWrapper(argument);
        }
    }
}
