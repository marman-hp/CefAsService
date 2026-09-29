using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace Xilium.CefGlue.Headless
{
    public static class JpegFrameEncoder
    {
        public static byte[] Encode(byte[] bgraBuffer, int width, int height, long quality = 80L)
        {
            var handle = GCHandle.Alloc(bgraBuffer, GCHandleType.Pinned);

            try
            {
                using var bitmap = new Bitmap(width, height, width * 4, PixelFormat.Format32bppArgb, handle.AddrOfPinnedObject());

                var jpegCodec = GetJpegCodecInfo();
                using var encoderParameters = new EncoderParameters(1);
                encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);

                using var stream = new MemoryStream();
                bitmap.Save(stream, jpegCodec, encoderParameters);
                return stream.ToArray();
            }
            finally
            {
                handle.Free();
            }
        }

        public static byte[] DecodeToBgra(byte[] jpegBytes, int width, int height)
        {
            using var stream = new MemoryStream(jpegBytes);
            using var bitmap = new Bitmap(stream);
            var bgraBuffer = new byte[width * height * 4];

            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);
            try
            {
                Marshal.Copy(bitmapData.Scan0, bgraBuffer, 0, bgraBuffer.Length);
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }

            return bgraBuffer;
        }

        private static ImageCodecInfo _jpegCodec;

        private static ImageCodecInfo GetJpegCodecInfo()
        {
            if (_jpegCodec != null)
            {
                return _jpegCodec;
            }

            foreach (var codec in ImageCodecInfo.GetImageEncoders())
            {
                if (codec.FormatID == ImageFormat.Jpeg.Guid)
                {
                    _jpegCodec = codec;
                    break;
                }
            }

            return _jpegCodec;
        }
    }
}
