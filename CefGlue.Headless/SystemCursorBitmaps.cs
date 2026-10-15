using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Xilium.CefGlue.Headless
{
    internal static class SystemCursorBitmaps
    {
        internal sealed class CursorBitmap
        {
            public int Width;
            public int Height;
            public int HotspotX;
            public int HotspotY;

            public byte[] Pixels;

            public byte[] Invert;
        }

        private const int OcrNormal = 32512, OcrIBeam = 32513, OcrWait = 32514, OcrCross = 32515,
            OcrSizeNwse = 32642, OcrSizeNesw = 32643, OcrSizeWe = 32644, OcrSizeNs = 32645, OcrSizeAll = 32646,
            OcrNo = 32648, OcrHand = 32649, OcrAppStarting = 32650, OcrHelp = 32651;

        private static readonly ConcurrentDictionary<(int Id, int Size), CursorBitmap> Cache = new();

        private static int _baseSize;
        private static long _baseSizeReadMs = long.MinValue;

        public static int BaseSize
        {
            get
            {
                var now = Environment.TickCount64;
                if (now - _baseSizeReadMs < 5000)
                {
                    return _baseSize;
                }

                var size = 32;
                if (OperatingSystem.IsWindows())
                {
                    try
                    {
                        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors");
                        if (key?.GetValue("CursorBaseSize") is int value && value >= 16 && value <= 256)
                        {
                            size = value;
                        }
                    }
                    catch (Exception)
                    {
                    }
                }

                _baseSize = size;
                _baseSizeReadMs = now;
                return size;
            }
        }

        public static int SystemCursorId(CefCursorType type) => type switch
        {
            CefCursorType.None => 0,
            CefCursorType.Cross or CefCursorType.Cell => OcrCross,
            CefCursorType.Hand or CefCursorType.Grab or CefCursorType.Grabbing or CefCursorType.DragAndDropLink => OcrHand,
            CefCursorType.IBeam or CefCursorType.VerticalText => OcrIBeam,
            CefCursorType.Wait => OcrWait,
            CefCursorType.Progress => OcrAppStarting,
            CefCursorType.Help => OcrHelp,
            CefCursorType.EastResize or CefCursorType.WestResize or CefCursorType.EastWestResize or CefCursorType.ColumnResize => OcrSizeWe,
            CefCursorType.NorthResize or CefCursorType.SouthResize or CefCursorType.NorthSouthResize or CefCursorType.RowResize => OcrSizeNs,
            CefCursorType.NorthEastResize or CefCursorType.SouthWestResize or CefCursorType.NorthEastSouthWestResize => OcrSizeNesw,
            CefCursorType.NorthWestResize or CefCursorType.SouthEastResize or CefCursorType.NorthWestSouthEastResize => OcrSizeNwse,
            CefCursorType.Move or CefCursorType.MiddlePanning or CefCursorType.MiddlePanningVertical or CefCursorType.MiddlePanningHorizontal
                or CefCursorType.EastPanning or CefCursorType.NorthPanning or CefCursorType.NorthEastPanning or CefCursorType.NorthWestPanning
                or CefCursorType.SouthPanning or CefCursorType.SouthEastPanning or CefCursorType.SouthWestPanning or CefCursorType.WestPanning
                or CefCursorType.DragAndDropMove => OcrSizeAll,
            CefCursorType.NotAllowed or CefCursorType.NoDrop or CefCursorType.DragAndDropNone => OcrNo,
            _ => OcrNormal,
        };

        public static CursorBitmap Load(int id, int size)
        {
            if (!OperatingSystem.IsWindows() || id == 0 || size <= 0)
            {
                return null;
            }

            return Cache.GetOrAdd((id, size), key => Extract(key.Id, key.Size));
        }

        private static CursorBitmap Extract(int id, int size)
        {
            var cursor = LoadImageW(IntPtr.Zero, (IntPtr)id, ImageCursor, size, size, 0);
            if (cursor == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                if (!GetIconInfo(cursor, out var info))
                {
                    return null;
                }

                try
                {
                    return info.hbmColor != IntPtr.Zero
                        ? FromColor(info.hbmColor, info.hbmMask, info.xHotspot, info.yHotspot)
                        : FromMonochrome(info.hbmMask, info.xHotspot, info.yHotspot);
                }
                finally
                {
                    if (info.hbmColor != IntPtr.Zero) DeleteObject(info.hbmColor);
                    if (info.hbmMask != IntPtr.Zero) DeleteObject(info.hbmMask);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SystemCursorBitmaps] cursor {id} at {size}px: {ex.Message}");
                return null;
            }
            finally
            {
                DestroyCursor(cursor);
            }
        }

        private static CursorBitmap FromColor(IntPtr color, IntPtr mask, int hotX, int hotY)
        {
            var (w, h, pixels) = Read32(color);
            if (pixels == null) return null;

            var hasAlpha = false;
            for (var i = 3; i < pixels.Length; i += 4)
            {
                if (pixels[i] != 0) { hasAlpha = true; break; }
            }

            if (!hasAlpha && mask != IntPtr.Zero)
            {
                var (mw, mh, maskPixels) = Read32(mask);
                if (maskPixels != null && mw == w && mh >= h)
                {
                    for (var i = 0; i < w * h; i++)
                    {
                        pixels[i * 4 + 3] = maskPixels[i * 4] == 0 ? (byte)255 : (byte)0;
                    }
                }
            }

            return new CursorBitmap { Width = w, Height = h, HotspotX = hotX, HotspotY = hotY, Pixels = pixels, Invert = new byte[w * h] };
        }

        private static CursorBitmap FromMonochrome(IntPtr mask, int hotX, int hotY)
        {
            var (w, h2, maskPixels) = Read32(mask);
            if (maskPixels == null || h2 < 2) return null;
            var h = h2 / 2;
            var pixels = new byte[w * h * 4];
            var invert = new byte[w * h];
            for (var i = 0; i < w * h; i++)
            {
                var and = maskPixels[i * 4] != 0;
                var xor = maskPixels[(i + w * h) * 4] != 0;
                var o = i * 4;
                if (!and)
                {
                    var v = xor ? (byte)255 : (byte)0;
                    pixels[o] = v; pixels[o + 1] = v; pixels[o + 2] = v; pixels[o + 3] = 255;
                }
                else if (xor)
                {
                    invert[i] = 1;
                }
            }

            return new CursorBitmap { Width = w, Height = h, HotspotX = hotX, HotspotY = hotY, Pixels = pixels, Invert = invert };
        }

        private static unsafe (int W, int H, byte[] Pixels) Read32(IntPtr bitmap)
        {
            var bm = default(BITMAP);
            if (GetObjectW(bitmap, Marshal.SizeOf<BITMAP>(), ref bm) == 0 || bm.bmWidth <= 0 || bm.bmHeight <= 0)
            {
                return (0, 0, null);
            }

            var header = new BITMAPINFOHEADER
            {
                biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = bm.bmWidth,
                biHeight = -bm.bmHeight,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = 0,
            };
            var pixels = new byte[bm.bmWidth * bm.bmHeight * 4];
            var dc = GetDC(IntPtr.Zero);
            try
            {
                fixed (byte* p = pixels)
                {
                    if (GetDIBits(dc, bitmap, 0, (uint)bm.bmHeight, (IntPtr)p, ref header, 0) == 0)
                    {
                        return (0, 0, null);
                    }
                }
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, dc);
            }

            return (bm.bmWidth, bm.bmHeight, pixels);
        }

        private const uint ImageCursor = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct ICONINFO
        {
            [MarshalAs(UnmanagedType.Bool)] public bool fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAP
        {
            public int bmType;
            public int bmWidth;
            public int bmHeight;
            public int bmWidthBytes;
            public ushort bmPlanes;
            public ushort bmBitsPixel;
            public IntPtr bmBits;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr LoadImageW(IntPtr hInst, IntPtr name, uint type, int cx, int cy, uint fuLoad);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyCursor(IntPtr hCursor);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern int GetObjectW(IntPtr h, int c, ref BITMAP pv);

        [DllImport("gdi32.dll")]
        private static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint cLines, IntPtr lpvBits, ref BITMAPINFOHEADER lpbmi, uint usage);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr ho);
    }
}
