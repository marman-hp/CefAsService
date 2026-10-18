namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefZipReader
    {
        public static CefZipReader Create(CefStreamReader stream)
        {
            if (stream == null) throw new ArgumentNullException("stream");

            return CefZipReader.FromNative(
                cef_zip_reader_t.create(stream.ToNative())
                );
        }

        public bool MoveToFirstFile()
        {
            return cef_zip_reader_t.move_to_first_file(_self) != 0;
        }

        public bool MoveToNextFile()
        {
            return cef_zip_reader_t.move_to_next_file(_self) != 0;
        }

        public bool MoveToFile(string fileName, bool caseSensitive)
        {
            fixed (char* fileName_str = fileName)
            {
                var n_fileName = new cef_string_t(fileName_str, fileName != null ? fileName.Length : 0);

                return cef_zip_reader_t.move_to_file(_self, &n_fileName, caseSensitive ? 1 : 0) != 0;
            }
        }

        public bool Close()
        {
            return cef_zip_reader_t.close(_self) != 0;
        }

        public string GetFileName()
        {
            var n_result = cef_zip_reader_t.get_file_name(_self);
            return cef_string_userfree.ToString(n_result);
        }

        public long GetFileSize()
        {
            return cef_zip_reader_t.get_file_size(_self);
        }

        public CefBaseTime GetFileLastModified()
        {
            return cef_zip_reader_t.get_file_last_modified(_self);
        }

        public bool OpenFile(string password)
        {
            fixed (char* password_str = password)
            {
                var n_password = new cef_string_t(password_str, password != null ? password.Length : 0);
                return cef_zip_reader_t.open_file(_self, &n_password) != 0;
            }
        }

        public bool CloseFile()
        {
            return cef_zip_reader_t.close_file(_self) != 0;
        }

        public int ReadFile(byte[] buffer, int offset, int length)
        {
            if (offset < 0 || length < 0 || buffer.Length - offset < length) throw new ArgumentOutOfRangeException();

            fixed (byte* buffer_ptr = buffer)
            {
                return cef_zip_reader_t.read_file(_self, buffer_ptr + offset, (UIntPtr)length);
            }
        }

        public long Tell()
        {
            return cef_zip_reader_t.tell(_self);
        }

        public bool Eof()
        {
            return cef_zip_reader_t.eof(_self) != 0;
        }
    }
}
