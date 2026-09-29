namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefDragData
    {
        public static CefDragData Create()
        {
            return CefDragData.FromNative(cef_drag_data_t.create());
        }

        public CefDragData Clone()
        {
            return CefDragData.FromNative(cef_drag_data_t.clone(_self));
        }

        public bool IsReadOnly
        {
            get { return cef_drag_data_t.is_read_only(_self) != 0; }
        }

        public bool IsLink
        {
            get { return cef_drag_data_t.is_link(_self) != 0; }
        }

        public bool IsFragment
        {
            get { return cef_drag_data_t.is_fragment(_self) != 0; }
        }

        public bool IsFile
        {
            get { return cef_drag_data_t.is_file(_self) != 0; }
        }

        public string LinkUrl
        {
            get
            {
                var n_result = cef_drag_data_t.get_link_url(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string LinkTitle
        {
            get
            {
                var n_result = cef_drag_data_t.get_link_title(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string LinkMetadata
        {
            get
            {
                var n_result = cef_drag_data_t.get_link_metadata(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string FragmentText
        {
            get
            {
                var n_result = cef_drag_data_t.get_fragment_text(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string FragmentHtml
        {
            get
            {
                var n_result = cef_drag_data_t.get_fragment_html(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string FragmentBaseUrl
        {
            get
            {
                var n_result = cef_drag_data_t.get_fragment_base_url(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public string FileName
        {
            get
            {
                var n_result = cef_drag_data_t.get_file_name(_self);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public ulong GetFileContents(CefStreamWriter writer)
        {
            var n_writer = writer != null ? writer.ToNative() : null;
            return (ulong)cef_drag_data_t.get_file_contents(_self, n_writer);
        }

        public string[] GetFileNames()
        {
            cef_string_list* n_result = null;
            try
            {
                n_result = libcef.string_list_alloc();
                var success = cef_drag_data_t.get_file_names(_self, n_result) != 0;
                if (!success) return null;
                return cef_string_list.ToArray(n_result);
            }
            finally
            {
                if (n_result != null) libcef.string_list_free(n_result);
            }
        }

        public string[] GetFilePaths()
        {
            cef_string_list* n_result = null;
            try
            {
                n_result = libcef.string_list_alloc();
                var success = cef_drag_data_t.get_file_paths(_self, n_result) != 0;
                if (!success) return null;
                return cef_string_list.ToArray(n_result);
            }
            finally
            {
                if (n_result != null) libcef.string_list_free(n_result);
            }
        }

        public void SetLinkURL(string url)
        {
            fixed (char* url_str = url)
            {
                var n_url = new cef_string_t(url_str, url != null ? url.Length : 0);
                cef_drag_data_t.set_link_url(_self, &n_url);
            }
        }

        public void SetLinkTitle(string title)
        {
            fixed (char* title_str = title)
            {
                var n_title = new cef_string_t(title_str, title != null ? title.Length : 0);
                cef_drag_data_t.set_link_title(_self, &n_title);
            }
        }

        public void SetLinkMetadata(string data)
        {
            fixed (char* data_str = data)
            {
                var n_data = new cef_string_t(data_str, data != null ? data.Length : 0);
                cef_drag_data_t.set_link_metadata(_self, &n_data);
            }
        }

        public void SetFragmentText(string text)
        {
            fixed (char* text_str = text)
            {
                var n_text = new cef_string_t(text_str, text != null ? text.Length : 0);
                cef_drag_data_t.set_fragment_text(_self, &n_text);
            }
        }

        public void SetFragmentHtml(string html)
        {
            fixed (char* html_str = html)
            {
                var n_html = new cef_string_t(html_str, html != null ? html.Length : 0);
                cef_drag_data_t.set_fragment_html(_self, &n_html);
            }
        }

        public void SetFragmentBaseURL(string baseUrl)
        {
            fixed (char* baseUrl_str = baseUrl)
            {
                var n_baseUrl = new cef_string_t(baseUrl_str, baseUrl != null ? baseUrl.Length : 0);
                cef_drag_data_t.set_fragment_base_url(_self, &n_baseUrl);
            }
        }

        public void ResetFileContents()
        {
            cef_drag_data_t.reset_file_contents(_self);
        }

        public void AddFile(string path, string displayName)
        {
            fixed (char* path_str = path)
            fixed (char* displayName_str = displayName)
            {
                var n_path = new cef_string_t(path_str, path != null ? path.Length : 0);
                var n_displayName = new cef_string_t(displayName_str, displayName != null ? displayName.Length : 0);

                cef_drag_data_t.add_file(_self, &n_path, &n_displayName);
            }
        }

        public void ClearFilenames()
            => cef_drag_data_t.clear_filenames(_self);

        public CefImage GetImage()
        {
            var result = cef_drag_data_t.get_image(_self);
            return CefImage.FromNativeOrNull(result);
        }

        public CefPoint GetImageHotspot()
        {
            var result = cef_drag_data_t.get_image_hotspot(_self);
            return new CefPoint(result.x, result.y);
        }

        public bool HasImage
        {
            get
            {
                return cef_drag_data_t.has_image(_self) != 0;
            }
        }
    }
}
