namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefBinaryValue : ICefBinaryValue
    {
        public static CefBinaryValue Create(byte[] data)
        {
            if (data == null) throw new ArgumentNullException("data");

            fixed (byte* data_ptr = data)
            {
                var value = cef_binary_value_t.create(data_ptr, (UIntPtr)data.LongLength);
                return CefBinaryValue.FromNative(value);
            }
        }

        public bool IsValid
        {
            get { return cef_binary_value_t.is_valid(_self) != 0; }
        }

        public bool IsOwned
        {
            get { return cef_binary_value_t.is_owned(_self) != 0; }
        }

        public bool IsSame(ICefBinaryValue that)
        {
            return cef_binary_value_t.is_same(_self, ((CefBinaryValue)that).ToNative()) != 0;
        }

        public bool IsEqual(ICefBinaryValue that)
        {
            return cef_binary_value_t.is_equal(_self, ((CefBinaryValue)that).ToNative()) != 0;
        }

        public ICefBinaryValue Copy()
        {
            var value = cef_binary_value_t.copy(_self);
            return CefBinaryValue.FromNative(value);
        }

        public UIntPtr RawData
        {
            get { return (UIntPtr)cef_binary_value_t.get_raw_data(_self); }
        }

        public long Size
        {
            get { return (long)cef_binary_value_t.get_size(_self); }
        }

        public long GetData(byte[] buffer, long bufferSize, long dataOffset)
        {
            if (buffer.LongLength < dataOffset + bufferSize) throw new ArgumentOutOfRangeException("dataOffset");

            fixed (byte* buffer_ptr = buffer)
            {
                return (long)cef_binary_value_t.get_data(_self, buffer_ptr, (UIntPtr)bufferSize, (UIntPtr)dataOffset);
            }

        }

        public byte[] ToArray()
        {
            var value = new byte[Size];
            var readed = GetData(value, value.Length, 0);
            if (readed != value.Length) throw new InvalidOperationException();
            return value;
        }
    }
}
