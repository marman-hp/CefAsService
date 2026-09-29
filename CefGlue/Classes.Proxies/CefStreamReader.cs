namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefStreamReader
    {
        public static CefStreamReader Create(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) throw new ArgumentNullException("fileName");

            fixed (char* fileName_str = fileName)
            {
                var n_fileName = new cef_string_t(fileName_str, fileName != null ? fileName.Length : 0);

                return CefStreamReader.FromNative(
                    cef_stream_reader_t.create_for_file(&n_fileName)
                    );
            }
        }

        public static CefStreamReader Create(void* data, long size)
        {
            return CefStreamReader.FromNative(
                cef_stream_reader_t.create_for_data(data, (UIntPtr)size)
                );
        }

        public static CefStreamReader Create(CefReadHandler handler)
        {
            if (handler == null) throw new ArgumentNullException("handler");

            return CefStreamReader.FromNative(
                cef_stream_reader_t.create_for_handler(handler.ToNative())
                );
        }

        public int Read(byte[] buffer, int offset, int length)
        {
            if (offset < 0 || length < 0 || buffer.Length - offset < length) throw new ArgumentOutOfRangeException();

            fixed (byte* ptr = &buffer[offset])
            {
                return (int)cef_stream_reader_t.read(_self, ptr, (UIntPtr)1, (UIntPtr)length);
            }
        }

        public bool Seek(long offset, SeekOrigin whence)
        {
            return cef_stream_reader_t.seek(_self, offset, (int)whence) == 0;
        }

        public long Tell()
        {
            return cef_stream_reader_t.tell(_self);
        }

        public bool Eof()
        {
            return cef_stream_reader_t.eof(_self) != 0;
        }

        public bool MayBlock()
        {
            return cef_stream_reader_t.may_block(_self) != 0;
        }
    }
}
