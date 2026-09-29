using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xilium.CefGlue.Common;
using Xilium.CefGlue.Common.Composition;
using Xilium.CefGlue.Common.Helpers;
using Xilium.CefGlue.Common.Platform;

namespace Xilium.CefGlue.Headless
{
    public sealed class HeadlessCefBrowser : BaseCefBrowser
    {
        private readonly HeadlessTarget _target;

        public HeadlessTarget Target => _target;

        public int? BrowserId => UnderlyingBrowser?.Identifier;

        public HeadlessCefBrowser(int width = 1280, int height = 720,
                                   Func<CefRequestContext> cefRequestContextFactory = null,
                                   Func<CefWindowInfo> setupCefWindowInfo = null,
                                   Func<CefBrowserSettings> setupCefBrowserSettings = null,
                                   CefGlueHost host = null)
            : this(width, height, cefRequestContextFactory, setupCefWindowInfo, setupCefBrowserSettings, host, isPopup: false)
        {
        }

        private HeadlessCefBrowser(int width, int height,
                                    Func<CefRequestContext> cefRequestContextFactory,
                                    Func<CefWindowInfo> setupCefWindowInfo,
                                    Func<CefBrowserSettings> setupCefBrowserSettings,
                                    CefGlueHost host,
                                    bool isPopup)
            : base(cefRequestContextFactory, host)
        {
            if (!CefRuntimeLoader.IsOSREnabled)
            {
                throw new NotSupportedException(
                    "CefGlue.Headless requires off-screen (windowless) rendering - set " +
                    "CefSettings.WindowlessRenderingEnabled = true before initializing CEF.");
            }

            _target = new HeadlessTarget(width, height);

            base.AttachTarget(_target);

            Adapter.SetupCefWindowInfo = () => setupCefWindowInfo?.Invoke() ?? CefWindowInfo.Create();
            Adapter.SetupCefBrowserSettings = setupCefBrowserSettings;

            if (isPopup)
            {
                return;
            }

            _target.Resize(width, height);
        }

        public static HeadlessCefBrowser CreateForPopup(int width = 1280, int height = 720,
                                                          Func<CefWindowInfo> setupCefWindowInfo = null,
                                                          Func<CefBrowserSettings> setupCefBrowserSettings = null,
                                                          CefGlueHost host = null)
            => new HeadlessCefBrowser(width, height, null, setupCefWindowInfo, setupCefBrowserSettings, host, isPopup: true);

        public void OnBeforeCreatePopup(ref CefClient client, ref CefDictionaryValue extraInfo, CefWindowInfo windowInfo, CefPopupFeatures popupFeatures)
        {
            Adapter.OnBeforeCreatePopup(ref client, ref extraInfo, windowInfo, popupFeatures);
        }

        public void CloseBrowser()
        {
            UnderlyingBrowser?.GetHost()?.CloseBrowser(true);
        }

        private static int _devToolsMessageId;

        public bool SendDevToolsMessage(string method, object @params = null)
        {
            var envelope = new Dictionary<string, object>
            {
                ["id"] = Interlocked.Increment(ref _devToolsMessageId),
                ["method"] = method,
            };

            if (@params != null)
            {
                envelope["params"] = @params;
            }

            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope));
            return UnderlyingBrowser?.GetHost()?.SendDevToolsMessage(bytes) ?? false;
        }

        protected internal override ITarget CreateControl() =>
            throw new NotSupportedException("CefGlue.Headless is off-screen only - there is no windowed mode.");

        protected internal override IOffScreenTargetHost CreateOffScreenControlHost() =>
            throw new InvalidOperationException("HeadlessTarget is self-hosting - CreateOffScreenControlHost should never be called for CefGlue.Headless.");

        protected internal override IOffScreenPopupHost CreatePopupHost() =>
            new HeadlessPopupHost((HeadlessRenderSurface)_target.RenderSurface, () => UnderlyingBrowser);
    }
}
