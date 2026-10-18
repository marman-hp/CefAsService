using System;
using SkiaSharp;

namespace Xilium.CefGlue.Headless
{
    public static class WebPFrameEncoder
    {
        public static byte[] Encode(byte[] bgraBuffer, int width, int height, int quality = 100)
        {
            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);

            unsafe
            {
                fixed (byte* pixels = bgraBuffer)
                {
                    using var bitmap = new SKBitmap();
                    bitmap.InstallPixels(info, (IntPtr)pixels, info.RowBytes);

                    using var image = SKImage.FromBitmap(bitmap);
                    using var data = image.Encode(SKEncodedImageFormat.Webp, quality);

                    return data.ToArray();
                }
            }
        }
    }
}
