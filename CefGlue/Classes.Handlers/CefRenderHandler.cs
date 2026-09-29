namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefRenderHandler
    {
        private static readonly CefRectangle[] s_emptyRectangleArray = new CefRectangle[0];

        private cef_accessibility_handler_t* get_accessibility_handler(cef_render_handler_t* self)
        {
            CheckSelf(self);
            var result = GetAccessibilityHandler();
            if (result == null) return null;
            return result.ToNative();
        }

        protected abstract CefAccessibilityHandler GetAccessibilityHandler();

        private int get_root_screen_rect(cef_render_handler_t* self, cef_browser_t* browser, cef_rect_t* rect)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var m_rect = new CefRectangle();

            var result = GetRootScreenRect(m_browser, ref m_rect);

            if (result)
            {
                rect->x = m_rect.X;
                rect->y = m_rect.Y;
                rect->width = m_rect.Width;
                rect->height = m_rect.Height;
                return 1;
            }
            else return 0;
        }

        protected virtual bool GetRootScreenRect(CefBrowser browser, ref CefRectangle rect)
        {
            return false;
        }

        private void get_view_rect(cef_render_handler_t* self, cef_browser_t* browser, cef_rect_t* rect)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            CefRectangle m_rect;

            GetViewRect(m_browser, out m_rect);

            rect->x = m_rect.X;
            rect->y = m_rect.Y;
            rect->width = m_rect.Width;
            rect->height = m_rect.Height;
        }

        protected abstract void GetViewRect(CefBrowser browser, out CefRectangle rect);

        private int get_screen_point(cef_render_handler_t* self, cef_browser_t* browser, int viewX, int viewY, int* screenX, int* screenY)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);

            int m_screenX = 0;
            int m_screenY = 0;

            var result = GetScreenPoint(m_browser, viewX, viewY, ref m_screenX, ref m_screenY);

            if (result)
            {
                *screenX = m_screenX;
                *screenY = m_screenY;
                return 1;
            }
            else return 0;
        }

        protected virtual bool GetScreenPoint(CefBrowser browser, int viewX, int viewY, ref int screenX, ref int screenY)
        {
            return false;
        }

        private int get_screen_info(cef_render_handler_t* self, cef_browser_t* browser, cef_screen_info_t* screen_info)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var m_screenInfo = new CefScreenInfo(screen_info);

            var result = GetScreenInfo(m_browser, m_screenInfo);

            m_screenInfo.Dispose();
            m_browser.Dispose();

            return result ? 1 : 0;
        }

        protected abstract bool GetScreenInfo(CefBrowser browser, CefScreenInfo screenInfo);

        private void on_popup_show(cef_render_handler_t* self, cef_browser_t* browser, int show)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);

            OnPopupShow(m_browser, show != 0);
        }

        protected virtual void OnPopupShow(CefBrowser browser, bool show)
        {
        }

        private void on_popup_size(cef_render_handler_t* self, cef_browser_t* browser, cef_rect_t* rect)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var m_rect = new CefRectangle(rect->x, rect->y, rect->width, rect->height);

            OnPopupSize(m_browser, m_rect);
        }

        protected abstract void OnPopupSize(CefBrowser browser, CefRectangle rect);

        private void on_paint(cef_render_handler_t* self, cef_browser_t* browser, CefPaintElementType type, UIntPtr dirtyRectsCount, cef_rect_t* dirtyRects, void* buffer, int width, int height)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);

            var m_dirtyRects = new CefRectangle[(int)dirtyRectsCount];

            var count = (int)dirtyRectsCount;
            var rect = dirtyRects;
            for (var i = 0; i < count; i++)
            {
                m_dirtyRects[i].X = rect->x;
                m_dirtyRects[i].Y = rect->y;
                m_dirtyRects[i].Width = rect->width;
                m_dirtyRects[i].Height = rect->height;

                rect++;
            }

            OnPaint(m_browser, type, m_dirtyRects, (IntPtr)buffer, width, height);
        }

        protected abstract void OnPaint(CefBrowser browser, CefPaintElementType type, CefRectangle[] dirtyRects, IntPtr buffer, int width, int height);

        private void on_accelerated_paint(cef_render_handler_t* self, cef_browser_t* browser, CefPaintElementType type, UIntPtr dirtyRectsCount, cef_rect_t* dirtyRects, cef_accelerated_paint_info_t* info)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);

            var m_dirtyRects = new CefRectangle[(int)dirtyRectsCount];

            var count = (int)dirtyRectsCount;
            var rect = dirtyRects;
            for (var i = 0; i < count; i++)
            {
                m_dirtyRects[i].X = rect->x;
                m_dirtyRects[i].Y = rect->y;
                m_dirtyRects[i].Width = rect->width;
                m_dirtyRects[i].Height = rect->height;

                rect++;
            }

            var m_Info = CefAcceleratedPaintInfo.FromNative(info);
            OnAcceleratedPaint(m_browser, type, m_dirtyRects, m_Info);
        }

        protected abstract void OnAcceleratedPaint(CefBrowser browser, CefPaintElementType type, CefRectangle[] dirtyRects, CefAcceleratedPaintInfo info);

        private void get_touch_handle_size(cef_render_handler_t* self, cef_browser_t* browser, CefHorizontalAlignment orientation, cef_size_t* size)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            CefSize mSize;
            GetTouchHandleSize(mBrowser, orientation, out mSize);
            size->width = mSize.Width;
            size->height = mSize.Height;
        }

        protected virtual void GetTouchHandleSize(CefBrowser browser, CefHorizontalAlignment orientation, out CefSize size)
            => size = default;

        private void on_touch_handle_state_changed(cef_render_handler_t* self, cef_browser_t* browser, cef_touch_handle_state_t* state)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            OnTouchHandleStateChanged(mBrowser, new CefTouchHandleState(state));
        }

        protected virtual void OnTouchHandleStateChanged(CefBrowser browser, CefTouchHandleState state)
        { }

        private int start_dragging(cef_render_handler_t* self, cef_browser_t* browser, cef_drag_data_t* drag_data, CefDragOperationsMask allowed_ops, int x, int y)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var m_dragData = CefDragData.FromNative(drag_data);

            var m_result = StartDragging(m_browser, m_dragData, allowed_ops, x, y);

            return m_result ? 1 : 0;
        }

        protected virtual bool StartDragging(CefBrowser browser, CefDragData dragData, CefDragOperationsMask allowedOps, int x, int y)
        {
            return false;
        }

        private void update_drag_cursor(cef_render_handler_t* self, cef_browser_t* browser, CefDragOperationsMask operation)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);

            UpdateDragCursor(m_browser, operation);
        }

        protected virtual void UpdateDragCursor(CefBrowser browser, CefDragOperationsMask operation)
        {
        }

        private void on_scroll_offset_changed(cef_render_handler_t* self, cef_browser_t* browser, double x, double y)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);

            OnScrollOffsetChanged(m_browser, x, y);
        }

        protected abstract void OnScrollOffsetChanged(CefBrowser browser, double x, double y);

        private void on_ime_composition_range_changed(cef_render_handler_t* self, cef_browser_t* browser, cef_range_t* selected_range, UIntPtr character_boundsCount, cef_rect_t* character_bounds)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var m_selectedRange = new CefRange(selected_range->from, selected_range->to);

            CefRectangle[] m_characterBounds;
            if (character_boundsCount == UIntPtr.Zero)
            {
                m_characterBounds = s_emptyRectangleArray;
            }
            else
            {
                var m_characterBoundsCount = checked((int)character_boundsCount);
                m_characterBounds = new CefRectangle[m_characterBoundsCount];
                for (var i = 0; i < m_characterBoundsCount; i++)
                {
                    m_characterBounds[i] = new CefRectangle(
                        character_bounds[i].x,
                        character_bounds[i].y,
                        character_bounds[i].width,
                        character_bounds[i].height
                        );
                }
            }

            OnImeCompositionRangeChanged(m_browser, m_selectedRange, m_characterBounds);
        }

        protected abstract void OnImeCompositionRangeChanged(CefBrowser browser, CefRange selectedRange, CefRectangle[] characterBounds);

        private void on_text_selection_changed(cef_render_handler_t* self, cef_browser_t* browser, cef_string_t* selected_text, cef_range_t* selected_range)
        {
            CheckSelf(self);

            var m_browser = CefBrowser.FromNative(browser);
            var m_selected_text = cef_string_t.ToString(selected_text);
            var m_selected_range = new CefRange(selected_range->from, selected_range->to);

            OnTextSelectionChanged(m_browser, m_selected_text, m_selected_range);
        }

        protected virtual void OnTextSelectionChanged(CefBrowser browser, string selectedText, CefRange selectedRange) { }

        private void on_virtual_keyboard_requested(cef_render_handler_t* self, cef_browser_t* browser, CefTextInputMode input_mode)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            OnVirtualKeyboardRequested(mBrowser, input_mode);
        }

        protected virtual void OnVirtualKeyboardRequested(CefBrowser browser, CefTextInputMode inputMode) { }
    }
}
