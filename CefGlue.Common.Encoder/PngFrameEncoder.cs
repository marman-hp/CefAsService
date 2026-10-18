using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace Xilium.CefGlue.Headless
{
    public static class PngFrameEncoder
    {
        public static byte[] Encode(byte[] bgraBuffer, int width, int height)
        {
            var handle = GCHandle.Alloc(bgraBuffer, GCHandleType.Pinned);

            try
            {
                using var bitmap = new Bitmap(width, height, width * 4, PixelFormat.Format32bppArgb, handle.AddrOfPinnedObject());
                using var stream = new MemoryStream();
                bitmap.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
            finally
            {
                handle.Free();
            }
        }
    }
}
