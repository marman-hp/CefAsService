namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefDisplayHandler
    {
        private void on_address_change(cef_display_handler_t* self, cef_browser_t* browser, cef_frame_t* frame, cef_string_t* url)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            var mFrame = CefFrame.FromNative(frame);
            var mUrl = cef_string_t.ToString(url);

            OnAddressChange(mBrowser, mFrame, mUrl);
        }

        protected virtual void OnAddressChange(CefBrowser browser, CefFrame frame, string url)
        {
        }

        private void on_title_change(cef_display_handler_t* self, cef_browser_t* browser, cef_string_t* title)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            var mTitle = cef_string_t.ToString(title);

            OnTitleChange(mBrowser, mTitle);
        }

        protected virtual void OnTitleChange(CefBrowser browser, string title)
        {
        }

        private void on_favicon_urlchange(cef_display_handler_t* self, cef_browser_t* browser, cef_string_list* icon_urls)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            var mIconUrls = cef_string_list.ToArray(icon_urls);

            OnFaviconUrlChange(mBrowser, mIconUrls);
        }

        protected virtual void OnFaviconUrlChange(CefBrowser browser, string[] iconUrls)
        {
        }

        private void on_fullscreen_mode_change(cef_display_handler_t* self, cef_browser_t* browser, int fullscreen)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            OnFullscreenModeChange(mBrowser, fullscreen != 0);
        }

        protected virtual void OnFullscreenModeChange(CefBrowser browser, bool fullscreen) { }

        private int on_tooltip(cef_display_handler_t* self, cef_browser_t* browser, cef_string_t* text)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            var mText = cef_string_t.ToString(text);

            return OnTooltip(mBrowser, mText) ? 1 : 0;
        }

        protected virtual bool OnTooltip(CefBrowser browser, string text)
        {
            return false;
        }

        private void on_status_message(cef_display_handler_t* self, cef_browser_t* browser, cef_string_t* value)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            var mValue = cef_string_t.ToString(value);

            OnStatusMessage(mBrowser, mValue);
        }

        protected virtual void OnStatusMessage(CefBrowser browser, string value)
        {
        }

        private int on_console_message(cef_display_handler_t* self, cef_browser_t* browser, CefLogSeverity level, cef_string_t* message, cef_string_t* source, int line)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            var mMessage = cef_string_t.ToString(message);
            var mSource = cef_string_t.ToString(source);

            return OnConsoleMessage(mBrowser, level, mMessage, mSource, line) ? 1 : 0;
        }

        protected virtual bool OnConsoleMessage(CefBrowser browser, CefLogSeverity level, string message, string source, int line)
        {
            return false;
        }

        private int on_auto_resize(cef_display_handler_t* self, cef_browser_t* browser, cef_size_t* new_size)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            var mNewSize = new CefSize(new_size->width, new_size->height);

            if(OnAutoResize(mBrowser, ref mNewSize))
            {
                new_size->width = mNewSize.Width;
                new_size->height = mNewSize.Height;
                return 1;
            }

            return 0;
        }

        protected virtual bool OnAutoResize(CefBrowser browser, ref CefSize newSize)
        {
            return false;
        }

        private void on_loading_progress_change(cef_display_handler_t* self, cef_browser_t* browser, double progress)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            OnLoadingProgressChange(mBrowser, progress);
        }

        protected virtual void OnLoadingProgressChange(CefBrowser browser, double progress) { }

        private int on_cursor_change(cef_display_handler_t* self, cef_browser_t* browser, IntPtr cursor, CefCursorType type, cef_cursor_info_t* custom_cursor_info)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var m_cefCursorInfo = type == CefCursorType.Custom ? new CefCursorInfo(custom_cursor_info) : null;

            var m_result = OnCursorChange(m_browser, cursor, type, m_cefCursorInfo);

            if (m_cefCursorInfo != null) m_cefCursorInfo.Dispose();
            return m_result ? 1 : 0;
        }

        protected virtual bool OnCursorChange(CefBrowser browser, IntPtr cursorHandle, CefCursorType type, CefCursorInfo customCursorInfo)
            => false;

        private void on_media_access_change(cef_display_handler_t* self, cef_browser_t* browser, int has_video_access, int has_audio_access)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);

            OnMediaAccessChange(mBrowser,
                hasVideoAccess: has_video_access != 0,
                hasAudioAccess: has_audio_access != 0);
        }

        protected virtual void OnMediaAccessChange(CefBrowser browser, bool hasVideoAccess, bool hasAudioAccess)
        { }

        private int on_contents_bounds_change(cef_display_handler_t* self, cef_browser_t* browser, cef_rect_t* new_bounds)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);

            return OnContentBoundChange(mBrowser, new CefRectangle(*new_bounds)) ? 1 : 0;
        }

        protected virtual bool OnContentBoundChange(CefBrowser browser, CefRectangle rectangle)
        {
            return false;
        }

        private int get_root_window_screen_rect(cef_display_handler_t* self, cef_browser_t* browser, cef_rect_t* rect)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);

            return OnGetRootWindowScreenRectangle(mBrowser, new CefRectangle(*rect)) ? 1 : 0;
        }

        protected virtual bool OnGetRootWindowScreenRectangle(CefBrowser browser, CefRectangle rectangle)
        {
            return false;
        }
    }
}
