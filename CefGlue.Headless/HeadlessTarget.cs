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

        public Task<CefDragOperationsMask> StartDrag(CefDragData dragData, CefDragOperationsMask allowedOps, int x, int y)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[HeadlessTarget] StartDrag: allowedOps={allowedOps} x={x} y={y} " +
                $"isLink={dragData?.IsLink} isFragment={dragData?.IsFragment} isFile={dragData?.IsFile} " +
                $"hasImage={dragData?.HasImage} linkURL={dragData?.LinkUrl}");

            _isDragActive = true;
            _allowedDragOps = allowedOps;
            _originalAllowedOps = allowedOps;
            _dragData = dragData;
            _needsDragReArm = false;
            _dragCompletionSource = new TaskCompletionSource<CefDragOperationsMask>();

            DragEnter?.Invoke(new CefMouseEvent(x, y, CefEventFlags.LeftMouseButton), dragData, allowedOps);

            return _dragCompletionSource.Task;
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

                DragOver?.Invoke(mouseEvent, _allowedDragOps);
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

                Drop?.Invoke(mouseEvent, ops);
                tcs?.TrySetResult(ops);
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
