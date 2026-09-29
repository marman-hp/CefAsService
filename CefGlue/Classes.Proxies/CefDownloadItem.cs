namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefDownloadItem
    {
        public bool IsValid
        {
            get { return cef_download_item_t.is_valid(_self) != 0; }
        }

        public bool IsInProgress
        {
            get { return cef_download_item_t.is_in_progress(_self) != 0; }
        }

        public bool IsComplete
        {
            get { return cef_download_item_t.is_complete(_self) != 0; }
        }

        public bool IsCanceled
        {
            get { return cef_download_item_t.is_canceled(_self) != 0; }
        }

        public long CurrentSpeed
        {
            get { return cef_download_item_t.get_current_speed(_self); }
        }

        public int PercentComplete
        {
            get { return cef_download_item_t.get_percent_complete(_self); }
        }

        public long TotalBytes
        {
            get { return cef_download_item_t.get_total_bytes(_self); }
        }

        public long ReceivedBytes
        {
            get { return cef_download_item_t.get_received_bytes(_self); }
        }

        public CefBaseTime StartTime
        {
            get
            {
                return cef_download_item_t.get_start_time(_self);
            }
        }

        public CefBaseTime EndTime
        {
            get
            {
                return cef_download_item_t.get_end_time(_self);
            }
        }

        public string FullPath
        {
            get
            {
                var n_result = cef_download_item_t.get_full_path(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public uint Id
        {
            get { return cef_download_item_t.get_id(_self); }
        }

        public string Url
        {
            get
            {
                var n_result = cef_download_item_t.get_url(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string OriginalUrl
        {
            get
            {
                var n_result = cef_download_item_t.get_original_url(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string SuggestedFileName
        {
            get
            {
                var n_result = cef_download_item_t.get_suggested_file_name(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string ContentDisposition
        {
            get
            {
                var n_result = cef_download_item_t.get_content_disposition(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string MimeType
        {
            get
            {
                var n_result = cef_download_item_t.get_mime_type(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public CefDownloadInterruptReason InterruptReason
        {
            get
            {
                return cef_download_item_t.get_interrupt_reason(_self);
            }
        }

        public bool IsInterrupted
        {
            get { return cef_download_item_t.is_interrupted(_self) != 0; }
        }
    }
}
