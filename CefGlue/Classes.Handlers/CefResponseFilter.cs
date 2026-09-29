namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefResponseFilter
    {
        private int init_filter(cef_response_filter_t* self)
        {
            CheckSelf(self);

            return InitFilter() ? 1 : 0;
        }

        protected abstract bool InitFilter();

        private CefResponseFilterStatus filter(cef_response_filter_t* self, void* data_in, UIntPtr data_in_size, UIntPtr* data_in_read, void* data_out, UIntPtr data_out_size, UIntPtr* data_out_written)
        {
            CheckSelf(self);

            UnmanagedMemoryStream m_in_stream = null;
            UnmanagedMemoryStream m_out_stream = null;
            try
            {
                if (data_in != null)
                {
                    m_in_stream = new UnmanagedMemoryStream((byte*)data_in, (long)data_in_size, (long)data_in_size, FileAccess.Read);
                }

                m_out_stream = new UnmanagedMemoryStream((byte*)data_out, 0, (long)data_out_size, FileAccess.Write);

                {
                    long m_inRead;
                    long m_outWritten;
                    var result = Filter(m_in_stream, (long)data_in_size, out m_inRead, m_out_stream, (long)data_out_size, out m_outWritten);
                    *data_in_read = (UIntPtr)m_inRead;
                    *data_out_written = (UIntPtr)m_outWritten;
                    return result;
                }
            }
            finally
            {
                m_out_stream?.Dispose();
                m_in_stream?.Dispose();
            }
        }

        protected abstract CefResponseFilterStatus Filter(UnmanagedMemoryStream dataIn, long dataInSize, out long dataInRead, UnmanagedMemoryStream dataOut, long dataOutSize, out long dataOutWritten);
    }
}
