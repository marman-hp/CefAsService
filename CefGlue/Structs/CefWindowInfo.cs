namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Xilium.CefGlue.Interop;
    using Xilium.CefGlue.Platform;
    using Xilium.CefGlue.Platform.Windows;

    public unsafe abstract class CefWindowInfo
    {
        public static CefWindowInfo Create()
        {
            switch (CefRuntime.Platform)
            {
                case CefRuntimePlatform.Windows: return new CefWindowInfoWindowsImpl();
                case CefRuntimePlatform.Linux: return new CefWindowInfoLinuxImpl();
                case CefRuntimePlatform.MacOS: return new CefWindowInfoMacImpl();
                default: throw new NotSupportedException();
            }
        }

        internal static CefWindowInfo FromNative(cef_window_info_t* ptr)
        {
            switch (CefRuntime.Platform)
            {
                case CefRuntimePlatform.Windows: return new CefWindowInfoWindowsImpl(ptr);
                case CefRuntimePlatform.Linux: return new CefWindowInfoLinuxImpl(ptr);
                case CefRuntimePlatform.MacOS: return new CefWindowInfoMacImpl(ptr);
                default: throw new NotSupportedException();
            }
        }

        private bool _own;
        private bool _disposed;

        protected internal CefWindowInfo(bool own)
        {
            _own = own;
        }

        ~CefWindowInfo()
        {
            Dispose();
        }

        internal void Dispose()
        {
            _disposed = true;
            if (_own)
            {
                DisposeNativePointer();
            }
            GC.SuppressFinalize(this);
        }

        internal cef_window_info_t* ToNative()
        {
            var ptr = GetNativePointer();
            _own = false;
            return ptr;
        }

        protected internal void ThrowIfDisposed()
        {
            if (_disposed) throw ExceptionBuilder.ObjectDisposed();
        }

        public bool Disposed => _disposed;

        internal abstract cef_window_info_t* GetNativePointer();
        protected internal abstract void DisposeNativePointer();

        public abstract IntPtr ParentHandle { get; set; }

        public abstract IntPtr Handle { get; set; }

        public abstract string Name { get; set; }
        public abstract CefRectangle Bounds { get; set; }

        public abstract WindowStyle Style { get; set; }
        public abstract WindowStyleEx StyleEx { get; set; }
        public abstract IntPtr MenuHandle { get; set; }

        public abstract bool Hidden { get; set; }

        public abstract bool WindowlessRenderingEnabled { get; set; }

        public abstract bool SharedTextureEnabled { get; set; }

        public abstract bool ExternalBeginFrameEnabled { get; set; }

        public abstract CefRuntimeStyle RuntimeStyle { get; set; }

        public void SetAsChild(IntPtr parentHandle, CefRectangle bounds)
        {
            ThrowIfDisposed();

            Style = WindowStyle.WS_CHILD
                  | WindowStyle.WS_CLIPCHILDREN
                  | WindowStyle.WS_CLIPSIBLINGS
                  | WindowStyle.WS_TABSTOP
                  | WindowStyle.WS_VISIBLE;

            ParentHandle = parentHandle;

            Bounds = bounds;
        }

        public void SetAsPopup(IntPtr parentHandle, string name)
        {
            ThrowIfDisposed();

            Style = WindowStyle.WS_OVERLAPPEDWINDOW
                  | WindowStyle.WS_CLIPCHILDREN
                  | WindowStyle.WS_CLIPSIBLINGS
                  | WindowStyle.WS_VISIBLE;

            ParentHandle = parentHandle;

            Bounds = new CefRectangle(
                int.MinValue,
                int.MinValue,
                int.MinValue,
                int.MinValue
                );

            Name = name;
        }

        public void SetAsWindowless(IntPtr parentHandle, bool transparent)
        {
            WindowlessRenderingEnabled = true;
            ParentHandle = parentHandle;
        }
    }
}
