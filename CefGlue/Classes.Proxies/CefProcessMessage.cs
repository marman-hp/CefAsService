namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefProcessMessage
    {
        public static CefProcessMessage Create(string name)
        {
            fixed (char* name_str = name)
            {
                var n_name = new cef_string_t(name_str, name != null ? name.Length : 0);
                return CefProcessMessage.FromNative(
                    cef_process_message_t.create(&n_name)
                    );
            }
        }

        public bool IsValid
        {
            get { return cef_process_message_t.is_valid(_self) != 0; }
        }

        public bool IsReadOnly
        {
            get { return cef_process_message_t.is_read_only(_self) != 0; }
        }

        public CefProcessMessage? Copy()
        {
            return CefProcessMessage.FromNativeOrNull(
                cef_process_message_t.copy(_self)
                );
        }

        public string Name
        {
            get
            {
                var n_result = cef_process_message_t.get_name(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public CefListValue? Arguments
        {
            get
            {
                return CefListValue.FromNativeOrNull(
                    cef_process_message_t.get_argument_list(_self)
                    );
            }
        }

        public CefSharedMemoryRegion? GetSharedMemoryRegion()
        {
            return CefSharedMemoryRegion.FromNativeOrNull(
                cef_process_message_t.get_shared_memory_region(_self)
                );
        }
    }
}
