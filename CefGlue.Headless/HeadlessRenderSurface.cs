using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Xilium.CefGlue.Common.Helpers;

namespace Xilium.CefGlue.Headless
{
    public sealed class HeadlessFrame
    {
        public byte[] Buffer { get; }
        public int Width { get; }
        public int Height { get; }
        public CefRectangle[] DirtyRects { get; }

        public HeadlessFrame(byte[] buffer, int width, int height, CefRectangle[] dirtyRects)
        {
            Buffer = buffer;
            Width = width;
            Height = height;
            DirtyRects = dirtyRects;
        }
    }

    internal sealed class HeadlessRenderSurface : CpuStagedRenderSurface
    {
        private int _renderedWidth;
        private int _renderedHeight;

        private byte[] _persistentBuffer;
        private bool _hasPendingUpdate;

        private readonly Stopwatch _fpsStopwatch = Stopwatch.StartNew();
        private int _frameCountSinceReset;
        private int _fps;

        public bool ShowOverlayInfo { get; set; }

        public string EncoderInfo { get; set; }

        private readonly object _popupLock = new object();
        private HeadlessFrame _popupFrame;
        private int _popupX;
        private int _popupY;
        private bool _popupVisible;

        public void UpdatePopupFrame(HeadlessFrame frame)
        {
            lock (_popupLock)
            {
                _popupFrame = frame;
            }
        }

        public void SetPopupRect(int x, int y, int width, int height)
        {
            lock (_popupLock)
            {
                _popupX = x;
                _popupY = y;
            }
        }

        public void SetPopupVisible(bool visible)
        {
            lock (_popupLock)
            {
                _popupVisible = visible;

                if (!visible)
                {
                    _popupFrame = null;
                }
            }

            RecompositeAndBroadcast();
        }

        public override bool AllowsTransparency => false;

        protected override int BytesPerPixel => 4;

        protected override int RenderedWidth => _renderedWidth;

        protected override int RenderedHeight => _renderedHeight;

        protected override bool UpdateDirtyRegionsOnly => true;

        public event Action<HeadlessFrame> FrameReady;

        protected override Task ExecuteInUIThread(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        protected override void CreateSurface(int width, int height)
        {
            _renderedWidth = width;
            _renderedHeight = height;
            _persistentBuffer = new byte[width * height * BytesPerPixel];
        }

        protected override Action BeginSurfaceUpdate()
        {
            _hasPendingUpdate = false;

            return () =>
            {
                if (!_hasPendingUpdate || _persistentBuffer == null)
                {
                    return;
                }

                var width = _renderedWidth;
                var height = _renderedHeight;
                var clean = (byte[])_persistentBuffer.Clone();

                lock (_cleanBufferLock)
                {
                    _lastCleanBuffer = clean;
                    _lastCleanWidth = width;
                    _lastCleanHeight = height;
                }

                FrameReady?.Invoke(Compose(clean, width, height));
            };
        }

        private readonly object _cleanBufferLock = new object();
        private byte[] _lastCleanBuffer;
        private int _lastCleanWidth;
        private int _lastCleanHeight;

        protected override unsafe void UpdateBitmap(IntPtr sourceBuffer, int sourceBufferSize, int stride, CefRectangle updateRegion)
        {
            if (sourceBuffer == IntPtr.Zero || sourceBufferSize <= 0 || stride <= 0 || _persistentBuffer == null)
            {
                return;
            }

            var x = Math.Max(0, updateRegion.X);
            var y = Math.Max(0, updateRegion.Y);
            var width = Math.Min(updateRegion.X + updateRegion.Width, _renderedWidth) - x;
            var height = Math.Min(updateRegion.Y + updateRegion.Height, _renderedHeight) - y;

            if (width <= 0 || height <= 0)
            {
                return;
            }

            var rowBytes = width * BytesPerPixel;
            var destStride = _renderedWidth * BytesPerPixel;
            var src = (byte*)sourceBuffer;

            for (var row = 0; row < height; row++)
            {
                var srcRowPtr = (IntPtr)(src + (long)(y + row) * stride + (long)x * BytesPerPixel);
                var destOffset = (y + row) * destStride + x * BytesPerPixel;
                Marshal.Copy(srcRowPtr, _persistentBuffer, destOffset, rowBytes);
            }

            _hasPendingUpdate = true;
        }

        private HeadlessFrame Compose(byte[] cleanBuffer, int width, int height)
        {
            var composited = (byte[])cleanBuffer.Clone();

            CompositePopup(composited, width, height);

            if (ShowOverlayInfo)
            {
                DrawOverlayInfo(composited, width, height);
            }

            return new HeadlessFrame(composited, width, height, new[] { new CefRectangle(0, 0, width, height) });
        }

        internal void RecompositeAndBroadcast()
        {
            byte[] cleanBuffer;
            int width, height;

            lock (_cleanBufferLock)
            {
                if (_lastCleanBuffer == null)
                {
                    return;
                }

                cleanBuffer = _lastCleanBuffer;
                width = _lastCleanWidth;
                height = _lastCleanHeight;
            }

            FrameReady?.Invoke(Compose(cleanBuffer, width, height));
        }

        private void CompositePopup(byte[] buffer, int width, int height)
        {
            HeadlessFrame popupFrame;
            int popupX, popupY;

            lock (_popupLock)
            {
                if (!_popupVisible || _popupFrame == null)
                {
                    return;
                }

                popupFrame = _popupFrame;
                popupX = _popupX;
                popupY = _popupY;
            }

            var copyWidth = Math.Min(popupFrame.Width, width - popupX);
            var copyHeight = Math.Min(popupFrame.Height, height - popupY);

            if (copyWidth <= 0 || copyHeight <= 0 || popupX < 0 || popupY < 0)
            {
                return;
            }

            var mainRowBytes = width * BytesPerPixel;
            var popupRowBytes = popupFrame.Width * BytesPerPixel;
            var copyRowBytes = copyWidth * BytesPerPixel;

            for (var y = 0; y < copyHeight; y++)
            {
                var srcOffset = y * popupRowBytes;
                var dstOffset = (popupY + y) * mainRowBytes + popupX * BytesPerPixel;

                System.Buffer.BlockCopy(popupFrame.Buffer, srcOffset, buffer, dstOffset, copyRowBytes);
            }
        }

        private void DrawOverlayInfo(byte[] buffer, int width, int height)
        {
            _frameCountSinceReset++;

            if (_fpsStopwatch.ElapsedMilliseconds >= 1000)
            {
                _fps = _frameCountSinceReset;
                _frameCountSinceReset = 0;
                _fpsStopwatch.Restart();
            }

            OverlayDiagnostics.EnsureBackgroundSampling();

            var coreLines = new[]
            {
                $"FPS         : {_fps}",
                $"CEF version : {OverlayDiagnostics.CefVersion}",
                $"Processor   : {OverlayDiagnostics.ProcessorName}",
                $"GPU         : {OverlayDiagnostics.GpuName}",
                $"Mem Usage   : {OverlayDiagnostics.MemoryUsageMb:F1} MB",
                $"CPU Usage   : {OverlayDiagnostics.CpuUsagePercent:F1} %",
                $"GPU Usage   : {OverlayDiagnostics.GpuUsagePercent:F1} %",
            };

            var lines = string.IsNullOrEmpty(EncoderInfo)
                ? coreLines
                : coreLines.Append($"Encoder     : {EncoderInfo}").ToArray();

            const int padding = 8;
            const int lineHeight = 16;
            var boxWidth = Math.Min(width, 230);
            var boxHeight = lines.Length * lineHeight + padding * 2 - 4;

            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

            try
            {
                using var bitmap = new Bitmap(width, height, width * BytesPerPixel, PixelFormat.Format32bppArgb, handle.AddrOfPinnedObject());
                using var g = Graphics.FromImage(bitmap);
                using var font = new Font(FontFamily.GenericMonospace, 11, GraphicsUnit.Pixel);
                using var boxBrush = new SolidBrush(Color.FromArgb(200, 30, 10, 45));
                using var textBrush = new SolidBrush(Color.FromArgb(235, 235, 235));

                var boxX = width - boxWidth - padding;
                g.FillRectangle(boxBrush, boxX, padding, boxWidth, boxHeight);

                for (var i = 0; i < lines.Length; i++)
                {
                    g.DrawString(lines[i], font, textBrush, boxX + padding, padding + 2 + i * lineHeight);
                }
            }
            finally
            {
                handle.Free();
            }
        }

        private static readonly D3D11 D3D11Api = D3D11.GetApi();

        private unsafe ID3D11Device* _d3dDevice;
        private unsafe ID3D11DeviceContext* _d3dContext;
        private unsafe ID3D11Device1* _d3dDevice1;
        private unsafe ID3D11Texture2D* _stagingTexture;

        private int _stagingWidth;
        private int _stagingHeight;
        private bool _stagingMapped;

        private readonly object _disposeLock = new object();
        private bool _disposed;

        protected override (int width, int height) GetAcceleratedFrameSize(CefAcceleratedPaintInfo info)
            => (info.Extra.VisibleRectangle.Width, info.Extra.VisibleRectangle.Height);

        protected override unsafe PixelBuffer ReadAcceleratedTexture(CefAcceleratedPaintInfo info)
        {
            lock (_disposeLock)
            {
                if (_disposed)
                {
                    return default;
                }

                return ReadAcceleratedTextureCore(info);
            }
        }

        private int _acceleratedPaintCount;

        private unsafe PixelBuffer ReadAcceleratedTextureCore(CefAcceleratedPaintInfo info)
        {
            EnsureD3DDevice();

            _acceleratedPaintCount++;
            if (_acceleratedPaintCount == 1 || _acceleratedPaintCount % 30 == 0)
            {
                Console.WriteLine($"[HeadlessRenderSurface] OnAcceleratedPaint firing (#{_acceleratedPaintCount}) - visible rect {info.Extra.VisibleRectangle.Width}x{info.Extra.VisibleRectangle.Height}.");
            }

            var textureGuid = ID3D11Texture2D.Guid;
            void* sharedTexturePointer = null;

            SilkMarshal.ThrowHResult(
                _d3dDevice1->OpenSharedResource1(
                    (void*)info.SharedTexture,
                    &textureGuid,
                    &sharedTexturePointer));

            var sharedTexture = (ID3D11Texture2D*)sharedTexturePointer;

            try
            {
                Texture2DDesc description;
                sharedTexture->GetDesc(&description);

                var codedWidth = (int)description.Width;
                var codedHeight = (int)description.Height;

                if (codedWidth <= 0 || codedHeight <= 0)
                {
                    return default;
                }

                EnsureStagingTexture(description);

                _d3dContext->CopyResource(
                    (ID3D11Resource*)_stagingTexture,
                    (ID3D11Resource*)sharedTexture);

                MappedSubresource mapped;

                SilkMarshal.ThrowHResult(
                    _d3dContext->Map(
                        (ID3D11Resource*)_stagingTexture,
                        0,
                        Map.MapRead,
                        0,
                        &mapped));

                _stagingMapped = true;

                var stride = (int)mapped.RowPitch;

                if (mapped.PData == null || stride <= 0)
                {
                    return default;
                }

                var visible = info.Extra.VisibleRectangle;
                var cropX = visible.X;
                var cropY = visible.Y;
                var cropWidth = visible.Width;
                var cropHeight = visible.Height;

                if (cropWidth <= 0 || cropHeight <= 0 ||
                    cropX < 0 || cropY < 0 ||
                    cropX + cropWidth > codedWidth || cropY + cropHeight > codedHeight)
                {
                    cropX = 0;
                    cropY = 0;
                    cropWidth = codedWidth;
                    cropHeight = codedHeight;
                }

                var byteOffset = (long)cropY * stride + (long)cropX * BytesPerPixel;
                var byteCount = (long)stride * cropHeight;

                if (byteCount <= 0 || byteCount > int.MaxValue)
                {
                    return default;
                }

                return new PixelBuffer(
                    (IntPtr)((byte*)mapped.PData + byteOffset),
                    cropWidth,
                    cropHeight,
                    stride,
                    (int)byteCount);
            }
            finally
            {
                sharedTexture->Release();
            }
        }

        protected override unsafe void ReleaseAcceleratedTexture()
        {
            lock (_disposeLock)
            {
                if (_disposed || !_stagingMapped)
                {
                    return;
                }

                _d3dContext->Unmap((ID3D11Resource*)_stagingTexture, 0);
                _stagingMapped = false;
            }
        }

        private unsafe void EnsureD3DDevice()
        {
            if (_d3dDevice != null)
            {
                return;
            }

            var gpuForcingEnabled = string.Equals(Environment.GetEnvironmentVariable("CEFGLUE_GPU_FORCING"), "1");

            ID3D11Device* device = null;
            ID3D11DeviceContext* context = null;

            if (gpuForcingEnabled && TryFindIntelAdapter(out var intelAdapter))
            {
                try
                {
                    SilkMarshal.ThrowHResult(
                        D3D11Api.CreateDevice(
                            (Silk.NET.DXGI.IDXGIAdapter*)intelAdapter,
                            D3DDriverType.Unknown,
                            Software: IntPtr.Zero,
                            Flags: (uint)CreateDeviceFlag.CreateDeviceBgraSupport,
                            pFeatureLevels: null,
                            FeatureLevels: 0,
                            SDKVersion: D3D11.SdkVersion,
                            ppDevice: &device,
                            pFeatureLevel: null,
                            ppImmediateContext: &context));
                }
                finally
                {
                    intelAdapter->Release();
                }
            }
            else
            {
                SilkMarshal.ThrowHResult(
                    D3D11Api.CreateDevice(
                        pAdapter: null,
                        D3DDriverType.D3DDriverTypeHardware,
                        Software: IntPtr.Zero,
                        Flags: (uint)CreateDeviceFlag.CreateDeviceBgraSupport,
                        pFeatureLevels: null,
                        FeatureLevels: 0,
                        SDKVersion: D3D11.SdkVersion,
                        ppDevice: &device,
                        pFeatureLevel: null,
                        ppImmediateContext: &context));
            }

            _d3dDevice = device;
            _d3dContext = context;

            var device1Guid = ID3D11Device1.Guid;
            void* device1Pointer = null;

            SilkMarshal.ThrowHResult(
                _d3dDevice->QueryInterface(&device1Guid, &device1Pointer));

            _d3dDevice1 = (ID3D11Device1*)device1Pointer;

            var dxgiDeviceGuid = IDXGIDevice.Guid;
            void* dxgiDevicePointer = null;
            SilkMarshal.ThrowHResult(_d3dDevice->QueryInterface(&dxgiDeviceGuid, &dxgiDevicePointer));
            var dxgiDevice = (IDXGIDevice*)dxgiDevicePointer;
            IDXGIAdapter* adapter = null;
            SilkMarshal.ThrowHResult(dxgiDevice->GetAdapter(&adapter));
            AdapterDesc desc;
            SilkMarshal.ThrowHResult(adapter->GetDesc(&desc));
            var adapterName = SilkMarshal.PtrToString((IntPtr)desc.Description, NativeStringEncoding.LPWStr);
            Console.WriteLine($"[HeadlessRenderSurface] Own D3D11 device resolved to: {adapterName} (LUID {desc.AdapterLuid.High}:{desc.AdapterLuid.Low}).");
            adapter->Release();
            dxgiDevice->Release();
        }

        private static unsafe bool TryFindIntelAdapter(out IDXGIAdapter1* intelAdapter)
        {
            intelAdapter = null;

            var dxgi = DXGI.GetApi(null);
            var factoryGuid = IDXGIFactory1.Guid;
            void* factoryPointer = null;
            SilkMarshal.ThrowHResult(dxgi.CreateDXGIFactory1(&factoryGuid, &factoryPointer));
            var factory = (IDXGIFactory1*)factoryPointer;

            try
            {
                uint i = 0;
                while (true)
                {
                    IDXGIAdapter1* candidate = null;
                    var hr = factory->EnumAdapters1(i, &candidate);
                    if (hr < 0 || candidate == null)
                    {
                        break;
                    }

                    AdapterDesc1 candidateDesc;
                    candidate->GetDesc1(&candidateDesc);
                    var name = SilkMarshal.PtrToString((IntPtr)candidateDesc.Description, NativeStringEncoding.LPWStr);

                    if (intelAdapter == null && name != null && name.Contains("Intel", StringComparison.OrdinalIgnoreCase))
                    {
                        intelAdapter = candidate;
                    }
                    else
                    {
                        candidate->Release();
                    }

                    i++;
                }

                return intelAdapter != null;
            }
            finally
            {
                factory->Release();
            }
        }

        private unsafe void EnsureStagingTexture(Texture2DDesc description)
        {
            var width = (int)description.Width;
            var height = (int)description.Height;

            if (_stagingTexture != null &&
                _stagingWidth == width &&
                _stagingHeight == height)
            {
                return;
            }

            if (_stagingTexture != null)
            {
                _stagingTexture->Release();
                _stagingTexture = null;
            }

            var stagingDescription = description;
            stagingDescription.Usage = Usage.UsageStaging;
            stagingDescription.BindFlags = 0;
            stagingDescription.CPUAccessFlags = (uint)CpuAccessFlag.CpuAccessRead;
            stagingDescription.MiscFlags = 0;

            ID3D11Texture2D* stagingTexture = null;

            SilkMarshal.ThrowHResult(
                _d3dDevice->CreateTexture2D(
                    &stagingDescription,
                    null,
                    &stagingTexture));

            _stagingTexture = stagingTexture;
            _stagingWidth = width;
            _stagingHeight = height;
        }

        public override unsafe void Dispose()
        {
            lock (_disposeLock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                if (_stagingMapped)
                {
                    _d3dContext->Unmap((ID3D11Resource*)_stagingTexture, 0);
                    _stagingMapped = false;
                }

                if (_stagingTexture != null)
                {
                    _stagingTexture->Release();
                    _stagingTexture = null;
                }

                if (_d3dDevice1 != null)
                {
                    _d3dDevice1->Release();
                    _d3dDevice1 = null;
                }

                if (_d3dContext != null)
                {
                    _d3dContext->Release();
                    _d3dContext = null;
                }

                if (_d3dDevice != null)
                {
                    _d3dDevice->Release();
                    _d3dDevice = null;
                }
            }

            base.Dispose();
        }
    }
}
