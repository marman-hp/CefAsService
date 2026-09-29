namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Xilium.CefGlue.Interop;

    public sealed unsafe class CefBrowserSettings
    {
        private cef_browser_settings_t* _self;

        public CefBrowserSettings()
        {
            _self = cef_browser_settings_t.Alloc();
        }

        internal CefBrowserSettings(cef_browser_settings_t* ptr)
        {
            _self = ptr;
        }

        internal void Dispose()
        {
            _self = null;
        }

        internal cef_browser_settings_t* ToNative()
        {
            return _self;
        }

        public int WindowlessFrameRate
        {
            get { return _self->windowless_frame_rate; }
            set { _self->windowless_frame_rate = value; }
        }

        #region Font Settings

        public string StandardFontFamily
        {
            get { return cef_string_t.ToString(&_self->standard_font_family); }
            set { cef_string_t.Copy(value, &_self->standard_font_family); }
        }

        public string FixedFontFamily
        {
            get { return cef_string_t.ToString(&_self->fixed_font_family); }
            set { cef_string_t.Copy(value, &_self->fixed_font_family); }
        }

        public string SerifFontFamily
        {
            get { return cef_string_t.ToString(&_self->serif_font_family); }
            set { cef_string_t.Copy(value, &_self->serif_font_family); }
        }

        public string SansSerifFontFamily
        {
            get { return cef_string_t.ToString(&_self->sans_serif_font_family); }
            set { cef_string_t.Copy(value, &_self->sans_serif_font_family); }
        }

        public string CursiveFontFamily
        {
            get { return cef_string_t.ToString(&_self->cursive_font_family); }
            set { cef_string_t.Copy(value, &_self->cursive_font_family); }
        }

        public string FantasyFontFamily
        {
            get { return cef_string_t.ToString(&_self->fantasy_font_family); }
            set { cef_string_t.Copy(value, &_self->fantasy_font_family); }
        }

        public int DefaultFontSize
        {
            get { return _self->default_font_size; }
            set { _self->default_font_size = value; }
        }

        public int DefaultFixedFontSize
        {
            get { return _self->default_fixed_font_size; }
            set { _self->default_fixed_font_size = value; }
        }

        public int MinimumFontSize
        {
            get { return _self->minimum_font_size; }
            set { _self->minimum_font_size = value; }
        }

        public int MinimumLogicalFontSize
        {
            get { return _self->minimum_logical_font_size; }
            set { _self->minimum_logical_font_size = value; }
        }

        #endregion

        public string DefaultEncoding
        {
            get { return cef_string_t.ToString(&_self->default_encoding); }
            set { cef_string_t.Copy(value, &_self->default_encoding); }
        }

        public CefState RemoteFonts
        {
            get { return _self->remote_fonts; }
            set { _self->remote_fonts = value; }
        }

        public CefState JavaScript
        {
            get { return _self->javascript; }
            set { _self->javascript = value; }
        }

        public CefState JavaScriptCloseWindows
        {
            get { return _self->javascript_close_windows; }
            set { _self->javascript_close_windows = value; }
        }

        public CefState JavaScriptAccessClipboard
        {
            get { return _self->javascript_access_clipboard; }
            set { _self->javascript_access_clipboard = value; }
        }

        public CefState JavaScriptDomPaste
        {
            get { return _self->javascript_dom_paste; }
            set { _self->javascript_dom_paste = value; }
        }

        public CefState ImageLoading
        {
            get { return _self->image_loading; }
            set { _self->image_loading = value; }
        }

        public CefState ImageShrinkStandaloneToFit
        {
            get { return _self->image_shrink_standalone_to_fit; }
            set { _self->image_shrink_standalone_to_fit = value; }
        }

        public CefState TextAreaResize
        {
            get { return _self->text_area_resize; }
            set { _self->text_area_resize = value; }
        }

        public CefState TabToLinks
        {
            get { return _self->tab_to_links; }
            set { _self->tab_to_links = value; }
        }

        public CefState LocalStorage
        {
            get { return _self->local_storage; }
            set { _self->local_storage = value; }
        }

        public CefState Databases
        {
            get { return _self->databases; }
            set { _self->databases = value; }
        }

        public CefState WebGL
        {
            get { return _self->webgl; }
            set { _self->webgl = value; }
        }

        public CefColor BackgroundColor
        {
            get { return new CefColor(_self->background_color); }
            set { _self->background_color = value.ToArgb(); }
        }

        public CefState ChromeStatusBubble
        {
            get { return _self->chrome_status_bubble; }
            set { _self->chrome_status_bubble = value; }
        }

        public CefState ChromeZoomBubble
        {
            get { return _self->chrome_zoom_bubble; }
            set { _self->chrome_zoom_bubble = value; }
        }
    }
}
