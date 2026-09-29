namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefWriteHandler
    {
        private UIntPtr write(cef_write_handler_t* self, void* ptr, UIntPtr size, UIntPtr n)
        {
            CheckSelf(self);

            var length = (long)size * (long)n;
            using (var stream = new UnmanagedMemoryStream((byte*)ptr, length, length, FileAccess.Write))
            {
                return (UIntPtr)Write(stream, length);
            }
        }

        protected abstract long Write(Stream stream, long length);

        private int seek(cef_write_handler_t* self, long offset, int whence)
        {
            CheckSelf(self);

            return Seek(offset, (SeekOrigin)whence) ? 0 : -1;
        }

        protected abstract bool Seek(long offset, SeekOrigin whence);

        private long tell(cef_write_handler_t* self)
        {
            CheckSelf(self);

            return Tell();
        }

        protected abstract long Tell();

        private int flush(cef_write_handler_t* self)
        {
            CheckSelf(self);

            return Flush() ? 0 : -1;
        }

        protected abstract bool Flush();

        private int may_block(cef_write_handler_t* self)
        {
            CheckSelf(self);

            return MayBlock() ? 1 : 0;
        }

        protected abstract bool MayBlock();
    }
}
