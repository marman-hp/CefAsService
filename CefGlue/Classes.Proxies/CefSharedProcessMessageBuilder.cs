namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefSharedProcessMessageBuilder
    {
        public static CefSharedProcessMessageBuilder Create(string name, nuint byteSize)
        {
            fixed (char* name_str = name)
            {
                var n_name = new cef_string_t(name_str, name != null ? name.Length : 0);
                var n_result = cef_shared_process_message_builder_t.create(&n_name, byteSize);
                return CefSharedProcessMessageBuilder.FromNative(n_result);
            }
        }

        public bool IsValid
        {
            get => cef_shared_process_message_builder_t.is_valid(_self) != 0;
        }

        public nuint Size
        {
            get => cef_shared_process_message_builder_t.size(_self);
        }

        public IntPtr Memory()
        {
            return (IntPtr)cef_shared_process_message_builder_t.memory(_self);
        }

        public CefProcessMessage Build()
        {
            return CefProcessMessage.FromNativeOrNull(
                cef_shared_process_message_builder_t.build(_self)
                );
        }
    }
}
