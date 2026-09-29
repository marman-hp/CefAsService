namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefPrintSettings
    {
        public static CefPrintSettings Create()
        {
            return CefPrintSettings.FromNative(
                cef_print_settings_t.create()
                );
        }

        public bool IsValid
        {
            get
            {
                return cef_print_settings_t.is_valid(_self) != 0;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return cef_print_settings_t.is_read_only(_self) != 0;
            }
        }

        public void SetOrientation(bool landscape)
        {
            cef_print_settings_t.set_orientation(_self, landscape ? 1 : 0);
        }

        public bool IsLandscape()
        {
            return cef_print_settings_t.is_landscape(_self) != 0;
        }

        public void SetPrinterPrintableArea(CefSize physicalSizeDeviceUnits, CefRectangle printableAreaDeviceUnits, bool landscapeNeedsFlip)
        {
            var n_physicalSizeDeviceUnits = new cef_size_t(
                physicalSizeDeviceUnits.Width,
                physicalSizeDeviceUnits.Height
                );

            var n_printableAreaDeviceUnits = new cef_rect_t(
                printableAreaDeviceUnits.X,
                printableAreaDeviceUnits.Y,
                printableAreaDeviceUnits.Width,
                printableAreaDeviceUnits.Height
                );

            cef_print_settings_t.set_printer_printable_area(
                _self,
                &n_physicalSizeDeviceUnits,
                &n_printableAreaDeviceUnits,
                landscapeNeedsFlip ? 1 : 0
                );
        }

        public void SetDeviceName(string name)
        {
            fixed (char* name_str = name)
            {
                var n_name = new cef_string_t(name_str, name != null ? name.Length : 0);
                cef_print_settings_t.set_device_name(_self, &n_name);
            }
        }

        public string DeviceName
        {
            get
            {
                var n_result = cef_print_settings_t.get_device_name(_self);
                if (n_result == null) return null;
                return cef_string_userfree.ToString(n_result);
            }
        }

        public void SetDpi(int dpi)
        {
            cef_print_settings_t.set_dpi(_self, dpi);
        }

        public int GetDpi()
        {
            return cef_print_settings_t.get_dpi(_self);
        }

        public void SetPageRanges(CefRange[] ranges)
        {
            var count = ranges != null ? ranges.Length : 0;
            var n_ranges = new cef_range_t[count];

            for (var i = 0; i < count; i++)
            {
                n_ranges[i].from = ranges[i].From;
                n_ranges[i].to = ranges[i].To;
            }

            fixed (cef_range_t* n_ranges_ptr = n_ranges)
            {
                cef_print_settings_t.set_page_ranges(_self, (UIntPtr)count, n_ranges_ptr);
            }
        }

        public int GetPageRangesCount()
        {
            return (int)cef_print_settings_t.get_page_ranges_count(_self);
        }

        public CefRange[] GetPageRanges()
        {
            var count = GetPageRangesCount();
            if (count == 0) return new CefRange[0];

            var n_ranges = new cef_range_t[count];
            UIntPtr n_count = (UIntPtr)count;
            fixed (cef_range_t* n_ranges_ptr = n_ranges)
            {
                cef_print_settings_t.get_page_ranges(_self, &n_count, n_ranges_ptr);
            }

            count = (int)n_count;
            if (count == 0) return new CefRange[0];

            var ranges = new CefRange[count];

            for (var i = 0; i < count; i++)
            {
                ranges[i].From = n_ranges[i].from;
                ranges[i].To = n_ranges[i].to;
            }

            return ranges;
        }

        public void SetSelectionOnly(bool selectionOnly)
        {
            cef_print_settings_t.set_selection_only(_self, selectionOnly ? 1 : 0);
        }

        public bool IsSelectionOnly
        {
            get
            {
                return cef_print_settings_t.is_selection_only(_self) != 0;
            }
        }

        public void SetCollate(bool collate)
        {
            cef_print_settings_t.set_collate(_self, collate ? 1 : 0);
        }

        public bool WillCollate
        {
            get
            {
                return cef_print_settings_t.will_collate(_self) != 0;
            }
        }

        public void SetColorModel(CefColorModel colorModel)
        {
            cef_print_settings_t.set_color_model(_self, colorModel);
        }

        public CefColorModel GetColorModel()
        {
            return cef_print_settings_t.get_color_model(_self);
        }

        public void SetCopies(int copies)
        {
            cef_print_settings_t.set_copies(_self, copies);
        }

        public int GetCopies()
        {
            return cef_print_settings_t.get_copies(_self);
        }

        public void SetDuplexMode(CefDuplexMode mode)
        {
            cef_print_settings_t.set_duplex_mode(_self, mode);
        }

        public CefDuplexMode GetDuplexMode()
        {
            return cef_print_settings_t.get_duplex_mode(_self);
        }
    }
}
