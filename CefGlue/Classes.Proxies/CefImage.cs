namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefImage
    {
        public static CefImage CreateImage()
        {
            return CefImage.FromNative(
                cef_image_t.create()
                );
        }

        public bool IsEmpty
        {
            get { return cef_image_t.is_empty(_self) != 0; }
        }

        public bool IsSame(CefImage that)
        {
            if (that == null) return false;
            return cef_image_t.is_same(_self, that.ToNative()) != 0;
        }

        public bool AddBitmap(float scaleFactor, int pixelWidth, int pixelHeight, CefColorType colorType, CefAlphaType alphaType, IntPtr pixelData, int pixelDataSize)
        {
            if (pixelData == IntPtr.Zero) throw new ArgumentNullException(nameof(pixelData));
            if (pixelDataSize < 0) throw new ArgumentOutOfRangeException(nameof(pixelDataSize));

            var n_result = cef_image_t.add_bitmap(_self, scaleFactor, pixelWidth, pixelHeight, colorType, alphaType, (void*)pixelData, (UIntPtr)pixelDataSize);
            return n_result != 0;
        }

        public bool AddPng(float scaleFactor, IntPtr pngData, int pngDataSize)
        {
            if (pngData == IntPtr.Zero) throw new ArgumentNullException(nameof(pngData));
            if (pngDataSize < 0) throw new ArgumentOutOfRangeException(nameof(pngDataSize));

            var n_result = cef_image_t.add_png(_self, scaleFactor, (void*)pngData, (UIntPtr)pngDataSize);
            return n_result != 0;
        }

        public bool AddJpeg(float scaleFactor, IntPtr jpegData, int jpegDataSize)
        {
            if (jpegData == IntPtr.Zero) throw new ArgumentNullException(nameof(jpegData));
            if (jpegDataSize < 0) throw new ArgumentOutOfRangeException(nameof(jpegDataSize));

            var n_result = cef_image_t.add_png(_self, scaleFactor, (void*)jpegData, (UIntPtr)jpegDataSize);
            return n_result != 0;
        }

        public int Width
        {
            get
            {
                var n_result = cef_image_t.get_width(_self);
                return checked((int)n_result);
            }
        }

        public int Height
        {
            get
            {
                var n_result = cef_image_t.get_height(_self);
                return checked((int)n_result);
            }
        }

        public bool HasRepresentation(float scaleFactor)
        {
            var n_result = cef_image_t.has_representation(_self, scaleFactor);
            return n_result != 0;
        }

        public bool RemoveRepresentation(float scaleFactor)
        {
            var n_result = cef_image_t.remove_representation(_self, scaleFactor);
            return n_result != 0;
        }

        public bool GetRepresentationInfo(float scaleFactor, out float actualScaleFactor, out int pixelWidth, out int pixelHeight)
        {
            float n_actualScaleFactor;
            int n_pixelWidth;
            int n_pixelHeight;
            var n_result = cef_image_t.get_representation_info(_self, scaleFactor, &n_actualScaleFactor, &n_pixelWidth, &n_pixelHeight);
            if (n_result != 0)
            {
                actualScaleFactor = n_actualScaleFactor;
                pixelWidth = n_pixelWidth;
                pixelHeight = n_pixelHeight;
                return true;
            }
            else
            {
                actualScaleFactor = 0;
                pixelWidth = 0;
                pixelHeight = 0;
                return false;
            }
        }

        public CefBinaryValue? GetAsBitmap(float scaleFactor, CefColorType colorType, CefAlphaType alphaType, out int pixelWidth, out int pixelHeight)
        {
            int n_pixelWidth;
            int n_pixelHeight;
            var n_result = cef_image_t.get_as_bitmap(_self, scaleFactor, colorType, alphaType, &n_pixelWidth, &n_pixelHeight);
            if (n_result != null)
            {
                pixelWidth = n_pixelWidth;
                pixelHeight = n_pixelHeight;
                return CefBinaryValue.FromNative(n_result);
            }
            else
            {
                pixelWidth = 0;
                pixelHeight = 0;
                return null;
            }
        }

        public CefBinaryValue? GetAsPng(float scaleFactor, bool withTransparency, out int pixelWidth, out int pixelHeight)
        {
            int n_pixelWidth;
            int n_pixelHeight;
            var n_result = cef_image_t.get_as_png(_self, scaleFactor, withTransparency ? 1 : 0, &n_pixelWidth, &n_pixelHeight);
            if (n_result != null)
            {
                pixelWidth = n_pixelWidth;
                pixelHeight = n_pixelHeight;
                return CefBinaryValue.FromNative(n_result);
            }
            else
            {
                pixelWidth = 0;
                pixelHeight = 0;
                return null;
            }
        }

        public CefBinaryValue GetAsJpeg(float scaleFactor, int quality, out int pixelWidth, out int pixelHeight)
        {
            int n_pixelWidth;
            int n_pixelHeight;
            var n_result = cef_image_t.get_as_jpeg(_self, scaleFactor, quality, &n_pixelWidth, &n_pixelHeight);
            if (n_result != null)
            {
                pixelWidth = n_pixelWidth;
                pixelHeight = n_pixelHeight;
                return CefBinaryValue.FromNative(n_result);
            }
            else
            {
                pixelWidth = 0;
                pixelHeight = 0;
                return null;
            }
        }
    }
}
