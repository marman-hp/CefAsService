using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xilium.CefGlue.Common.Helpers;
using Xilium.CefGlue.Common.Platform;

namespace Xilium.CefGlue.Headless
{
    public sealed class HeadlessTarget : IOffScreenTargetHost, IAnyTarget
    {
        private readonly HeadlessRenderSurface _renderSurface = new HeadlessRenderSurface();

        public HeadlessTarget(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "HeadlessTarget needs a real initial size - CEF can never produce a frame at 0x0.");
            }

            Width = width;
            Height = height;

            _renderSurface.Resize(width, height);
        }

        public int Width { get; private set; }
        public int Height { get; private set; }

        public OffScreenRenderSurface RenderSurface => _renderSurface;

        public void Resize(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            Width = width;
            Height = height;
            SizeChanged?.Invoke(new CefSize(width, height));
        }

        public event Action GotFocus;
        public event Action<CefSize> SizeChanged;

        public IntPtr? GetHostViewHandle(int initialWidth, int initialHeight) => IntPtr.Zero;

        public event Action<IEnumerable<MenuEntry>, int, int> ContextMenuOpened;
        public event Action ContextMenuClosed;

        private CefRunContextMenuCallback _pendingContextMenuCallback;

        public void OpenContextMenu(IEnumerable<MenuEntry> menuEntries, int x, int y, CefRunContextMenuCallback callback)
        {
            _pendingContextMenuCallback?.Cancel();
            _pendingContextMenuCallback = callback;
            ContextMenuOpened?.Invoke(menuEntries, x, y);
        }

        public void CloseContextMenu()
        {
            _pendingContextMenuCallback = null;
            ContextMenuClosed?.Invoke();
        }

        public void CompleteContextMenu(int? commandId, CefEventFlags eventFlags)
        {
            var callback = _pendingContextMenuCallback;
            _pendingContextMenuCallback = null;

            if (callback == null)
            {
                return;
            }

            if (commandId.HasValue)
            {
                callback.Continue(commandId.Value, eventFlags);
            }
            else
            {
                callback.Cancel();
            }
        }

        public void InitializeRender(IntPtr browserHandle) { }
        public void DestroyRender() { }

        public event Action<CefCursorType> CursorChanged;

        public bool SetCursor(IntPtr cursorHandle, CefCursorType cursorType)
        {
            CursorChanged?.Invoke(cursorType);
            return false;
        }

        public event Action<CefTextInputMode> VirtualKeyboardRequested;

        internal void RaiseVirtualKeyboardRequested(CefTextInputMode inputMode)
            => VirtualKeyboardRequested?.Invoke(inputMode);

        public event Action LostFocus;

        public event KeyEventHandler KeyDown;
        public event KeyEventHandler KeyUp;
        public event TextInputEventHandler TextInput;
        public event Action<string, int> CompositionUpdated;
        public event Action<string> CompositionCommitted;

        public event Action<IOffScreenTargetHost, CefMouseEvent, CefMouseButtonType, int> MouseButtonPressed;
        public event Action<CefMouseEvent, CefMouseButtonType> MouseButtonReleased;
        public event Action<CefMouseEvent> MouseLeave;
        public event Action<CefMouseEvent> MouseMoved;
        public event Action<CefMouseEvent, int, int> MouseWheelChanged;

        public event Action<CefMouseEvent, CefDragData, CefDragOperationsMask> DragEnter;
        public event Action<CefMouseEvent, CefDragOperationsMask> DragOver;
        public event Action DragLeave;
        public event Action<CefMouseEvent, CefDragOperationsMask> Drop;

        public event Action<float> ScreenInfoChanged;
        public event Action<bool> VisibilityChanged;

        public void Focus() => GotFocus?.Invoke();

        public CefPoint PointToScreen(CefPoint point, float deviceScaleFactor) => point;

        public void SetFullscreen(bool fullscreen) { }
        public void ReleaseMouseCapture() { }

        private bool _isDragActive;
        private CefDragOperationsMask _allowedDragOps;
        private CefDragOperationsMask _originalAllowedOps;
        private CefDragData _dragData;
        private bool _needsDragReArm;
        private TaskCompletionSource<CefDragOperationsMask> _dragCompletionSource;

        public sealed class DragGhostInfo
        {
            public byte[] Png;
            public int Width, Height, HotspotX, HotspotY;
            public string Label;
            public string Url;
            public string Text;
            public CefDragOperationsMask Ops;
        }

        public event Action<DragGhostInfo> DragStarted;
        public event Action<CefDragOperationsMask> DragCursorChanged;
        public event Action<bool> DragEnded;

        private const int MaxGhostPngBytes = 64 * 1024;
        private const int NoneDebounceMs = 150;
        private CefDragOperationsMask _publishedDragOps;
        private System.Threading.Timer _noneDebounce;

        private static DragGhostInfo BuildDragGhost(CefDragData dragData, CefDragOperationsMask ops)
        {
            var info = new DragGhostInfo { Ops = ops };
            try
            {
                if (dragData == null) return info;

                var label = !string.IsNullOrEmpty(dragData.FragmentText) ? dragData.FragmentText
                    : !string.IsNullOrEmpty(dragData.LinkUrl) ? dragData.LinkUrl
                    : null;
                if (label == null && dragData.IsFile)
                {
                    var names = dragData.GetFileNames();
                    if (names != null && names.Length > 0) label = names[0];
                }
                info.Label = label != null && label.Length > 120 ? label.Substring(0, 120) : label;

                var url = dragData.LinkUrl;
                if (string.IsNullOrEmpty(url))
                {
                    var t = dragData.FragmentText?.Trim();
                    if (!string.IsNullOrEmpty(t) && t.IndexOf(' ') < 0 && t.IndexOf('\n') < 0
                        && (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                            || t.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                    {
                        url = t;
                    }
                }
                info.Url = string.IsNullOrEmpty(url) ? null : url;
                var ft = dragData.FragmentText;
                info.Text = ft != null && ft.Length > 2000 ? ft.Substring(0, 2000) : ft;

                var img = dragData.GetImage();
                if (img != null)
                {
                    var hot = dragData.GetImageHotspot();
                    var png = img.GetAsPng(1f, true, out var pw, out var ph)?.ToArray();
                    if (png != null && png.Length > 0 && png.Length <= MaxGhostPngBytes)
                    {
                        info.Png = png;
                        info.Width = pw;
                        info.Height = ph;
                        info.HotspotX = hot.X;
                        info.HotspotY = hot.Y;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HeadlessTarget] BuildDragGhost failed: {ex.Message}");
            }
            return info;
        }

        private void EndDragGhost(bool dropped)
        {
            _noneDebounce?.Dispose();
            _noneDebounce = null;
            _publishedDragOps = CefDragOperationsMask.None;
            DragEnded?.Invoke(dropped);
        }

        public Task<CefDragOperationsMask> StartDrag(CefDragData dragData, CefDragOperationsMask allowedOps, int x, int y)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[HeadlessTarget] StartDrag: allowedOps={allowedOps} x={x} y={y} " +
                $"isLink={dragData?.IsLink} isFragment={dragData?.IsFragment} isFile={dragData?.IsFile} " +
                $"hasImage={dragData?.HasImage} linkURL={dragData?.LinkUrl}");

            var completion = new TaskCompletionSource<CefDragOperationsMask>(TaskCreationOptions.RunContinuationsAsynchronously);
            _publishedDragOps = allowedOps;
            _allowedDragOps = allowedOps;
            _originalAllowedOps = allowedOps;
            _dragData = dragData;
            _needsDragReArm = false;
            _dragCompletionSource = completion;
            _isDragActive = true;

            DragStarted?.Invoke(BuildDragGhost(dragData, allowedOps));

            if (completion.Task.IsCompleted)
            {
                EndDragGhost(completion.Task.Result != CefDragOperationsMask.None);
                return completion.Task;
            }

            DragEnter?.Invoke(new CefMouseEvent(x, y, CefEventFlags.LeftMouseButton), dragData, allowedOps);

            return completion.Task;
        }

        public void UpdateDragCursor(CefDragOperationsMask allowedOps)
        {
            if (allowedOps != _allowedDragOps)
            {
                System.Diagnostics.Debug.WriteLine($"[HeadlessTarget] UpdateDragCursor: {_allowedDragOps} -> {allowedOps}");
            }

            if (allowedOps == CefDragOperationsMask.None && _allowedDragOps != CefDragOperationsMask.None)
            {
                _needsDragReArm = true;
            }

            _allowedDragOps = allowedOps;

            if (_isDragActive)
            {
                if (allowedOps != CefDragOperationsMask.None)
                {
                    _noneDebounce?.Dispose();
                    _noneDebounce = null;
                    if (allowedOps != _publishedDragOps)
                    {
                        _publishedDragOps = allowedOps;
                        DragCursorChanged?.Invoke(allowedOps);
                    }
                }
                else if (_publishedDragOps != CefDragOperationsMask.None && _noneDebounce == null)
                {
                    _noneDebounce = new System.Threading.Timer(_ =>
                    {
                        if (_isDragActive && _allowedDragOps == CefDragOperationsMask.None
                            && _publishedDragOps != CefDragOperationsMask.None)
                        {
                            _publishedDragOps = CefDragOperationsMask.None;
                            DragCursorChanged?.Invoke(CefDragOperationsMask.None);
                        }
                        _noneDebounce?.Dispose();
                        _noneDebounce = null;
                    }, null, NoneDebounceMs, System.Threading.Timeout.Infinite);
                }
            }
        }

        public void CancelDragIfActive()
        {
            if (_isDragActive)
            {
                CancelDrag();
            }
        }

        private void CancelDrag()
        {
            System.Diagnostics.Debug.WriteLine("[HeadlessTarget] CancelDrag (Escape)");

            var tcs = _dragCompletionSource;
            _isDragActive = false;
            _dragCompletionSource = null;
            _dragOverCount = 0;
            _needsDragReArm = false;
            _dragData = null;
            DragLeave?.Invoke();
            tcs?.TrySetResult(CefDragOperationsMask.None);
            EndDragGhost(false);
        }

        private int _dragOverCount;

        public void InjectMouseMove(CefMouseEvent mouseEvent)
        {
            if (_isDragActive)
            {
                _dragOverCount++;

                if (_needsDragReArm)
                {
                    _needsDragReArm = false;
                    System.Diagnostics.Debug.WriteLine($"[HeadlessTarget] Re-arming drag session at x={mouseEvent.X} y={mouseEvent.Y} (originalAllowedOps={_originalAllowedOps})");
                    DragEnter?.Invoke(mouseEvent, _dragData, _originalAllowedOps);
                    return;
                }

                if (_dragOverCount == 1 || _dragOverCount % 20 == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[HeadlessTarget] DragOver #{_dragOverCount} x={mouseEvent.X} y={mouseEvent.Y} allowedOps={_allowedDragOps}");
                }

                DragOver?.Invoke(mouseEvent, _originalAllowedOps);
                return;
            }

            MouseMoved?.Invoke(mouseEvent);
        }

        public void InjectMouseButtonDown(CefMouseEvent mouseEvent, CefMouseButtonType button, int clickCount)
            => MouseButtonPressed?.Invoke(this, mouseEvent, button, clickCount);

        public void InjectMouseButtonUp(CefMouseEvent mouseEvent, CefMouseButtonType button)
        {
            if (_isDragActive)
            {
                System.Diagnostics.Debug.WriteLine($"[HeadlessTarget] Drop at x={mouseEvent.X} y={mouseEvent.Y} allowedOps={_allowedDragOps}");

                var ops = _allowedDragOps;
                var tcs = _dragCompletionSource;
                _isDragActive = false;
                _dragCompletionSource = null;
                _dragOverCount = 0;
                _needsDragReArm = false;
                _dragData = null;

                Drop?.Invoke(mouseEvent, _originalAllowedOps);
                tcs?.TrySetResult(ops);
                EndDragGhost(ops != CefDragOperationsMask.None);
                return;
            }

            MouseButtonReleased?.Invoke(mouseEvent, button);
        }

        public void InjectMouseLeave(CefMouseEvent mouseEvent) => MouseLeave?.Invoke(mouseEvent);

        public void InjectMouseWheel(CefMouseEvent mouseEvent, int deltaX, int deltaY)
            => MouseWheelChanged?.Invoke(mouseEvent, deltaX, deltaY);

        public void InjectKeyDown(CefKeyEvent keyEvent, out bool handled)
        {
            if (_isDragActive && keyEvent.WindowsKeyCode == 27  )
            {
                CancelDrag();
                handled = true;
                return;
            }

            var localHandled = false;
            KeyDown?.Invoke(keyEvent, out localHandled);
            handled = localHandled;
        }

        public void InjectKeyUp(CefKeyEvent keyEvent, out bool handled)
        {
            var localHandled = false;
            KeyUp?.Invoke(keyEvent, out localHandled);
            handled = localHandled;
        }

        public void InjectText(string text, out bool handled)
        {
            var localHandled = false;
            TextInput?.Invoke(text, out localHandled);
            handled = localHandled;
        }

        public void InjectFocus() => GotFocus?.Invoke();
        public void InjectLostFocus() => LostFocus?.Invoke();

        public void InjectVisibility(bool visible) => VisibilityChanged?.Invoke(visible);
    }
}
