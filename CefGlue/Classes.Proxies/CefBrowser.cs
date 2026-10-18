namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefBrowser
    {
        public bool IsValid => cef_browser_t.is_valid(_self) != 0;

        public CefBrowserHost GetHost()
        {
            return CefBrowserHost.FromNative(
                cef_browser_t.get_host(_self)
                );
        }

        public bool CanGoBack
        {
            get { return cef_browser_t.can_go_back(_self) != 0; }
        }

        public void GoBack()
        {
            cef_browser_t.go_back(_self);
        }

        public bool CanGoForward
        {
            get { return cef_browser_t.can_go_forward(_self) != 0; }
        }

        public void GoForward()
        {
            cef_browser_t.go_forward(_self);
        }

        public bool IsLoading
        {
            get { return cef_browser_t.is_loading(_self) != 0; }
        }

        public void Reload()
        {
            cef_browser_t.reload(_self);
        }

        public void ReloadIgnoreCache()
        {
            cef_browser_t.reload_ignore_cache(_self);
        }

        public void StopLoad()
        {
            cef_browser_t.stop_load(_self);
        }

        public int Identifier
        {
            get { return cef_browser_t.get_identifier(_self); }
        }

        public bool IsSame(CefBrowser that)
        {
            if (that == null) return false;
            return cef_browser_t.is_same(_self, that.ToNative()) != 0;
        }

        public bool IsPopup
        {
            get { return cef_browser_t.is_popup(_self) != 0; }
        }

        public bool HasDocument
        {
            get { return cef_browser_t.has_document(_self) != 0; }
        }

        public CefFrame GetMainFrame()
        {
            return CefFrame.FromNativeOrNull(
                cef_browser_t.get_main_frame(_self)
                );
        }

        public CefFrame GetFocusedFrame()
        {
            return CefFrame.FromNativeOrNull(
                cef_browser_t.get_focused_frame(_self)
                );
        }

        public CefFrame GetFrameByIdentifier(string identifier)
        {
            fixed (char* identifier_str = identifier)
            {
                var n_identifier = new cef_string_t(identifier_str, identifier.Length);

                return CefFrame.FromNativeOrNull(
                    cef_browser_t.get_frame_by_identifier(_self, &n_identifier)
                );
            }
        }

        public CefFrame GetFrameByName(string name)
        {
            fixed (char* name_str = name)
            {
                var n_name = new cef_string_t(name_str, name.Length);

                return CefFrame.FromNativeOrNull(
                    cef_browser_t.get_frame_by_name(_self, &n_name)
                    );
            }
        }

        public int FrameCount => (int)cef_browser_t.get_frame_count(_self);

        public string[] GetFrameIdentifiers()
        {
            var identifiers = libcef.string_list_alloc();
            cef_browser_t.get_frame_identifiers(_self, identifiers);
            return cef_string_list.ToArray(identifiers);
        }

        public string[] GetFrameNames()
        {
            var list = libcef.string_list_alloc();
            cef_browser_t.get_frame_names(_self, list);
            var result = cef_string_list.ToArray(list);
            libcef.string_list_free(list);
            return result;
        }
    }
}
