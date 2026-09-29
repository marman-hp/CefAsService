namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefPostDataElement
    {
        public static CefPostDataElement Create()
        {
            return CefPostDataElement.FromNative(
                cef_post_data_element_t.create()
                );
        }

        public bool IsReadOnly
        {
            get { return cef_post_data_element_t.is_read_only(_self) != 0; }
        }

        public void SetToEmpty()
        {
            cef_post_data_element_t.set_to_empty(_self);
        }

        public void SetToFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) throw new ArgumentException("Argument can't be null or empty.", "fileName");

            fixed (char* fileName_str = fileName)
            {
                var n_fileName = new cef_string_t(fileName_str, fileName.Length);
                cef_post_data_element_t.set_to_file(_self, &n_fileName);
            }
        }

        public void SetToBytes(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException("bytes");

            fixed (byte* bytes_ptr = bytes)
            {
                cef_post_data_element_t.set_to_bytes(_self, (UIntPtr)bytes.Length, bytes_ptr);
            }
        }

        public CefPostDataElementType ElementType
        {
            get { return cef_post_data_element_t.get_type(_self); }
        }

        public string GetFile()
        {
            var n_result = cef_post_data_element_t.get_file(_self);
            return cef_string_userfree.ToString(n_result);
        }

        public long BytesCount
        {
            get { return (long)cef_post_data_element_t.get_bytes_count(_self); }
        }

        public byte[] GetBytes()
        {
            var size = BytesCount;
            if (size == 0) return new byte[0];

            var bytes = new byte[size];
            fixed (byte* bytes_ptr = bytes)
            {
                var written = (long)cef_post_data_element_t.get_bytes(_self, (UIntPtr)size, bytes_ptr);
                if (written != size) throw new InvalidOperationException();
            }

            return bytes;
        }
    }
}
