namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefBrowserHost
    {
        public static void CreateBrowser(CefWindowInfo windowInfo, CefClient client, CefBrowserSettings settings, string url, CefDictionaryValue extraInfo = null, CefRequestContext requestContext = null)
        {
            if (windowInfo == null) throw new ArgumentNullException("windowInfo");
            if (client == null) throw new ArgumentNullException("client");
            if (settings == null) throw new ArgumentNullException("settings");

            var n_windowInfo = windowInfo.GetNativePointer();
            var n_client = client.ToNative();
            var n_settings = settings.ToNative();
            var n_extraInfo = extraInfo != null ? extraInfo.ToNative() : null;
            var n_requestContext = requestContext != null ? requestContext.ToNative() : null;

            fixed (char* url_ptr = url)
            {
                var n_url = new cef_string_t(url_ptr, url != null ? url.Length : 0);
                var n_success = cef_browser_host_t.create_browser(n_windowInfo, n_client, &n_url, n_settings, n_extraInfo, n_requestContext);
                if (n_success != 1) throw ExceptionBuilder.FailedToCreateBrowser(n_success);
            }
        }

        public static CefBrowser CreateBrowserSync(CefWindowInfo windowInfo, CefClient client, CefBrowserSettings settings, string url, CefDictionaryValue extraInfo = null, CefRequestContext requestContext = null)
        {
            if (windowInfo == null) throw new ArgumentNullException("windowInfo");
            if (client == null) throw new ArgumentNullException("client");
            if (settings == null) throw new ArgumentNullException("settings");

            var n_windowInfo = windowInfo.GetNativePointer();
            var n_client = client.ToNative();
            var n_settings = settings.ToNative();
            var n_extraInfo = extraInfo != null ? extraInfo.ToNative() : null;
            var n_requestContext = requestContext != null ? requestContext.ToNative() : null;

            fixed (char* url_ptr = url)
            {
                cef_string_t n_url = new cef_string_t(url_ptr, url != null ? url.Length : 0);
                var n_browser = cef_browser_host_t.create_browser_sync(n_windowInfo, n_client, &n_url, n_settings, n_extraInfo, n_requestContext);
                return CefBrowser.FromNative(n_browser);
            }
        }

        public CefBrowser GetBrowser()
        {
            return CefBrowser.FromNative(cef_browser_host_t.get_browser(_self));
        }

        public void CloseBrowser(bool forceClose = false)
        {
            cef_browser_host_t.close_browser(_self, forceClose ? 1 : 0);
        }

        public bool TryCloseBrowser()
        {
            return cef_browser_host_t.try_close_browser(_self) != 0;
        }

        public void SetFocus(bool focus)
        {
            cef_browser_host_t.set_focus(_self, focus ? 1 : 0);
        }

        public IntPtr GetWindowHandle()
        {
            return cef_browser_host_t.get_window_handle(_self);
        }

        public IntPtr GetOpenerWindowHandle()
        {
            return cef_browser_host_t.get_opener_window_handle(_self);
        }

        public bool HasView
        {
            get { return cef_browser_host_t.has_view(_self) != 0; }
        }

        public CefClient GetClient()
        {
            return CefClient.FromNative(
                cef_browser_host_t.get_client(_self)
            );
        }

        public CefRequestContext GetRequestContext()
        {
            return CefRequestContext.FromNative(
                cef_browser_host_t.get_request_context(_self)
            );
        }

        public bool CanZoom(CefZoomCommand command)
        {
            return cef_browser_host_t.can_zoom(_self, command) != 0;
        }

        public void Zoom(CefZoomCommand command)
        {
            cef_browser_host_t.zoom(_self, command);
        }

        public double GetDefaultZoomLevel()
        {
            return cef_browser_host_t.get_default_zoom_level(_self);
        }

        public double GetZoomLevel()
        {
            return cef_browser_host_t.get_zoom_level(_self);
        }

        public void SetZoomLevel(double value)
        {
            cef_browser_host_t.set_zoom_level(_self, value);
        }

        public void RunFileDialog(CefFileDialogMode mode, string title, string defaultFilePath, string[] acceptFilters, CefRunFileDialogCallback callback)
        {
            if (callback == null) throw new ArgumentNullException("callback");

            fixed (char* title_ptr = title)
            fixed (char* defaultFilePath_ptr = defaultFilePath)
            {
                var n_title = new cef_string_t(title_ptr, title != null ? title.Length : 0);
                var n_defaultFilePath = new cef_string_t(defaultFilePath_ptr, defaultFilePath != null ? defaultFilePath.Length : 0);
                var n_acceptFilters = cef_string_list.From(acceptFilters);

                cef_browser_host_t.run_file_dialog(_self, mode, &n_title, &n_defaultFilePath, n_acceptFilters, callback.ToNative());

                libcef.string_list_free(n_acceptFilters);
            }
        }

        public void StartDownload(string url)
        {
            if (string.IsNullOrEmpty(url)) throw new ArgumentNullException("url");

            fixed (char* url_ptr = url)
            {
                var n_url = new cef_string_t(url_ptr, url.Length);

                cef_browser_host_t.start_download(_self, &n_url);
            }
        }

        public void DownloadImage(string imageUrl, bool isFavIcon, uint maxImageSize, bool bypassCache, CefDownloadImageCallback callback)
        {
            if (string.IsNullOrEmpty(imageUrl)) throw new ArgumentNullException("imageUrl");

            fixed (char* imageUrl_ptr = imageUrl)
            {
                var n_imageUrl = new cef_string_t(imageUrl_ptr, imageUrl.Length);
                var n_callback = callback.ToNative();
                cef_browser_host_t.download_image(_self, &n_imageUrl, isFavIcon ? 1 : 0, maxImageSize, bypassCache ? 1 : 0, n_callback);
            }
        }

        public void Print()
        {
            cef_browser_host_t.print(_self);
        }

        public void PrintToPdf(string path, CefPdfPrintSettings settings, CefPdfPrintCallback callback)
        {
            fixed (char* path_ptr = path)
            {
                var n_path = new cef_string_t(path_ptr, path.Length);

                var n_settings = settings.ToNative();

                cef_browser_host_t.print_to_pdf(_self,
                    &n_path,
                    n_settings,
                    callback.ToNative()
                    );

                cef_pdf_print_settings_t.Clear(n_settings);
                cef_pdf_print_settings_t.Free(n_settings);
            }
        }

        public void Find(string searchText, bool forward, bool matchCase, bool findNext)
        {
            fixed (char* searchText_ptr = searchText)
            {
                var n_searchText = new cef_string_t(searchText_ptr, searchText.Length);

                cef_browser_host_t.find(_self, &n_searchText, forward ? 1 : 0, matchCase ? 1 : 0, findNext ? 1 : 0);
            }
        }

        public void StopFinding(bool clearSelection)
        {
            cef_browser_host_t.stop_finding(_self, clearSelection ? 1 : 0);
        }

        public void ShowDevTools(CefWindowInfo windowInfo, CefClient client, CefBrowserSettings browserSettings, CefPoint inspectElementAt)
        {
            var n_inspectElementAt = new cef_point_t(inspectElementAt.X, inspectElementAt.Y);
            cef_browser_host_t.show_dev_tools(_self, windowInfo.ToNative(), client.ToNative(), browserSettings.ToNative(),
                &n_inspectElementAt);
        }

        public void CloseDevTools()
        {
            cef_browser_host_t.close_dev_tools(_self);
        }

        public bool HasDevTools
        {
            get
            {
                return cef_browser_host_t.has_dev_tools(_self) != 0;
            }
        }

        public bool SendDevToolsMessage(IntPtr message, int messageSize)
        {
            return cef_browser_host_t.send_dev_tools_message(
                _self, (void*)message, checked((UIntPtr)messageSize)) != 0;
        }

        public int ExecuteDevToolsMethod(int messageId, string method, CefDictionaryValue parameters)
        {
            fixed (char* method_str = method)
            {
                var n_method = new cef_string_t(method_str, method != null ? method.Length : 0);

                return cef_browser_host_t.execute_dev_tools_method(
                    _self, messageId, &n_method, parameters.ToNative());
            }
        }

        public CefRegistration AddDevToolsMessageObserver(CefDevToolsMessageObserver observer)
        {
            if (observer == null) throw new ArgumentNullException(nameof(observer));

            var n_registration = cef_browser_host_t.add_dev_tools_message_observer(_self,
                observer.ToNative());
            return CefRegistration.FromNativeOrNull(n_registration);
        }

        public void GetNavigationEntries(CefNavigationEntryVisitor visitor, bool currentOnly)
        {
            cef_browser_host_t.get_navigation_entries(_self, visitor.ToNative(), currentOnly ? 1 : 0);
        }

        public void ReplaceMisspelling(string word)
        {
            fixed (char* word_str = word)
            {
                var n_word = new cef_string_t(word_str, word != null ? word.Length : 0);
                cef_browser_host_t.replace_misspelling(_self, &n_word);
            }
        }

        public void AddWordToDictionary(string word)
        {
            fixed (char* word_str = word)
            {
                var n_word = new cef_string_t(word_str, word != null ? word.Length : 0);
                cef_browser_host_t.add_word_to_dictionary(_self, &n_word);
            }
        }

        public bool IsWindowRenderingDisabled => cef_browser_host_t.is_window_rendering_disabled(_self) != 0;

        public void WasResized()
        {
            cef_browser_host_t.was_resized(_self);
        }

        public void WasHidden(bool hidden)
        {
            cef_browser_host_t.was_hidden(_self, hidden ? 1 : 0);
        }

        public void NotifyScreenInfoChanged()
        {
            cef_browser_host_t.notify_screen_info_changed(_self);
        }

        public void Invalidate(CefPaintElementType type)
        {
            cef_browser_host_t.invalidate(_self, type);
        }

        public void SendExternalBeginFrame()
        {
            cef_browser_host_t.send_external_begin_frame(_self);
        }

        public void SendKeyEvent(CefKeyEvent keyEvent)
        {
            if (keyEvent == null) throw new ArgumentNullException("keyEvent");

            var n_event = new cef_key_event_t();
            keyEvent.ToNative(&n_event);
            cef_browser_host_t.send_key_event(_self, &n_event);
        }

        public void SendMouseClickEvent(CefMouseEvent @event, CefMouseButtonType type, bool mouseUp, int clickCount)
        {
            var n_event = @event.ToNative();
            cef_browser_host_t.send_mouse_click_event(_self, &n_event, type, mouseUp ? 1 : 0, clickCount);
        }

        public void SendMouseMoveEvent(CefMouseEvent @event, bool mouseLeave)
        {
            var n_event = @event.ToNative();
            cef_browser_host_t.send_mouse_move_event(_self, &n_event, mouseLeave ? 1 : 0);
        }

        public void SendMouseWheelEvent(CefMouseEvent @event, int deltaX, int deltaY)
        {
            var n_event = @event.ToNative();
            cef_browser_host_t.send_mouse_wheel_event(_self, &n_event, deltaX, deltaY);
        }

        public void SendTouchEvent(CefTouchEvent @event)
        {
            cef_touch_event_t n_event;
            @event.ToNative(out n_event);
            cef_browser_host_t.send_touch_event(_self, &n_event);
        }

        public void SendCaptureLostEvent()
        {
            cef_browser_host_t.send_capture_lost_event(_self);
        }

        public void NotifyMoveOrResizeStarted()
        {
            cef_browser_host_t.notify_move_or_resize_started(_self);
        }

        public int GetWindowlessFrameRate()
        {
            return cef_browser_host_t.get_windowless_frame_rate(_self);
        }

        public void SetWindowlessFrameRate(int frameRate)
        {
            cef_browser_host_t.set_windowless_frame_rate(_self, frameRate);
        }

        public void ImeSetComposition(string text,
            int underlinesCount,
            CefCompositionUnderline underlines,
            CefRange replacementRange,
            CefRange selectionRange)
        {
            fixed (char* text_ptr = text)
            {
                cef_string_t n_text = new cef_string_t(text_ptr, text != null ? text.Length : 0);
                UIntPtr n_underlinesCount = checked((UIntPtr)underlinesCount);
                var n_underlines = underlines.ToNative();
                cef_range_t n_replacementRange = new cef_range_t(replacementRange.From, replacementRange.To);
                cef_range_t n_selectionRange = new cef_range_t(selectionRange.From, selectionRange.To);

                cef_browser_host_t.ime_set_composition(_self, &n_text, n_underlinesCount, &n_underlines, &n_replacementRange, &n_selectionRange);
            }
        }

        public void ImeCommitText(string text, CefRange replacementRange, int relativeCursorPos)
        {
            fixed (char* text_ptr = text)
            {
                cef_string_t n_text = new cef_string_t(text_ptr, text != null ? text.Length : 0);
                var n_replacementRange = new cef_range_t(replacementRange.From, replacementRange.To);
                cef_browser_host_t.ime_commit_text(_self, &n_text, &n_replacementRange, relativeCursorPos);
            }
        }

        public void ImeFinishComposingText(bool keepSelection)
        {
            cef_browser_host_t.ime_finish_composing_text(_self, keepSelection ? 1 : 0);
        }

        public void ImeCancelComposition()
        {
            cef_browser_host_t.ime_cancel_composition(_self);
        }

        public void DragTargetDragEnter(CefDragData dragData, CefMouseEvent mouseEvent, CefDragOperationsMask allowedOps)
        {
            var n_mouseEvent = mouseEvent.ToNative();
            cef_browser_host_t.drag_target_drag_enter(_self,
                dragData.ToNative(),
                &n_mouseEvent,
                allowedOps);
        }

        public void DragTargetDragOver(CefMouseEvent mouseEvent, CefDragOperationsMask allowedOps)
        {
            var n_mouseEvent = mouseEvent.ToNative();
            cef_browser_host_t.drag_target_drag_over(_self, &n_mouseEvent, allowedOps);
        }

        public void DragTargetDragLeave()
        {
            cef_browser_host_t.drag_target_drag_leave(_self);
        }

        public void DragTargetDrop(CefMouseEvent mouseEvent)
        {
            var n_mouseEvent = mouseEvent.ToNative();
            cef_browser_host_t.drag_target_drop(_self, &n_mouseEvent);
        }

        public void DragSourceEndedAt(int x, int y, CefDragOperationsMask op)
        {
            cef_browser_host_t.drag_source_ended_at(_self, x, y, op);
        }

        public void DragSourceSystemDragEnded()
        {
            cef_browser_host_t.drag_source_system_drag_ended(_self);
        }

        public CefNavigationEntry GetVisibleNavigationEntry()
        {
            return CefNavigationEntry.FromNativeOrNull(
                cef_browser_host_t.get_visible_navigation_entry(_self)
                );
        }

        public void SetAccessibilityState(CefState accessibilityState)
        {
            cef_browser_host_t.set_accessibility_state(_self, accessibilityState);
        }

        public void SetAutoResizeEnabled(bool enabled, CefSize minSize, CefSize maxSize)
        {
            var nMinSize = new cef_size_t(minSize.Width, minSize.Height);
            var nMaxSize = new cef_size_t(maxSize.Width, maxSize.Height);
            cef_browser_host_t.set_auto_resize_enabled(_self, enabled ? 1 : 0, &nMinSize, &nMaxSize);
        }

        public void SetAudioMuted(bool value)
        {
            cef_browser_host_t.set_audio_muted(_self, value ? 1 : 0);
        }

        public bool IsAudioMuted => cef_browser_host_t.is_audio_muted(_self) != 0;

        public bool IsFullscreen()
        {
            return cef_browser_host_t.is_fullscreen(_self) != 0;
        }

        public void ExitFullscreen(bool willCauseResize)
        {
            cef_browser_host_t.exit_fullscreen(_self, willCauseResize ? 1 : 0);
        }

        public bool CanExecuteChromeCommand(int commandId)
        {
            return cef_browser_host_t.can_execute_chrome_command(_self, commandId) != 0;
        }

        public void ExecuteChromeCommand(int commandId, CefWindowOpenDisposition disposition)
        {
            cef_browser_host_t.execute_chrome_command(_self, commandId, disposition);
        }
    }
}
