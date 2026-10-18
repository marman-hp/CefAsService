namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefReadHandler
    {
        private UIntPtr read(cef_read_handler_t* self, void* ptr, UIntPtr size, UIntPtr n)
        {
            CheckSelf(self);

            var length = (long)size * (long)n;
            using (var stream = new UnmanagedMemoryStream((byte*)ptr, length, length, FileAccess.Write))
            {
                return (UIntPtr)Read(stream, length);
            }
        }

        protected abstract long Read(Stream stream, long length);

        private int seek(cef_read_handler_t* self, long offset, int whence)
        {
            CheckSelf(self);

            return Seek(offset, (SeekOrigin)whence) ? 0 : -1;
        }

        protected abstract bool Seek(long offset, SeekOrigin whence);

        private long tell(cef_read_handler_t* self)
        {
            CheckSelf(self);

            return Tell();
        }

        protected abstract long Tell();

        private int eof(cef_read_handler_t* self)
        {
            CheckSelf(self);

            return Eof() ? 1 : 0;
        }

        protected abstract bool Eof();

        private int may_block(cef_read_handler_t* self)
        {
            CheckSelf(self);

            return MayBlock() ? 1 : 0;
        }

        protected abstract bool MayBlock();
    }
}
