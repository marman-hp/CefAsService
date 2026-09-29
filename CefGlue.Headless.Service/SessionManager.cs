using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xilium.CefGlue.Common;
using Xilium.CefGlue.Common.Composition;
using Xilium.CefGlue.Common.Events;
using Xilium.CefGlue.Headless;
using Xilium.CefGlue.Headless.Server;

namespace Xilium.CefGlue.Headless.Service
{
    internal sealed class SessionManager
    {
        private readonly Dictionary<string, BrowserSession> _sessions = new();
        private readonly IFrameTransport _frameSocketServer;

        private static readonly JsonSerializerOptions CamelCaseJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        private string _selectedId;
        private bool _disposed;
        private bool _overlayEnabled = true;

        private FramePixelFormat _frameFormat = FramePixelFormat.RawBgra;

        private VideoQuality _h264Quality = Program.DefaultH264Quality;

        private int _nextH264EncoderGeneration;

        private readonly IsolationContextStore _isolation = Program.IsolationContexts;

        public SessionManager(IFrameTransport frameSocketServer)
        {
            _frameSocketServer = frameSocketServer;
            _popupLifeSpanHandler = new HeadlessPopupLifeSpanHandler(this);
        }

        public string AddressBarValue { get; private set; } = Program.DefaultUrl;

        internal static string NormalizeStartUrl(string url)
        {
            url = url?.Trim();
            if (string.IsNullOrEmpty(url) || IsScriptUrl(url))
            {
                return null;
            }

            if (url.Contains("://") ||
                url.StartsWith("about:", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            return "https://" + url;
        }

        public void HandleMessage(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("action", out var actionProp))
                {
                    return;
                }

                switch (actionProp.GetString())
                {
                    case "add":
                        AddBrowser(Program.NewPageUsesLastUrl ? GetString(root, "url") : Program.DefaultUrl);
                        break;

                    case "remove":
                        RemoveBrowser(GetString(root, "id"));
                        break;

                    case "select":
                        Select(GetString(root, "id"));
                        break;

                    case "navigate":
                        NavigateAll(GetString(root, "url"));
                        break;

                    case "mousemove":
                        InjectMouseMove(GetInt(root, "x"), GetInt(root, "y"), (CefEventFlags)(uint)GetInt(root, "mod"));
                        break;

                    case "mousedown":
                    case "mouseup":
                        var isMouseDown = actionProp.GetString() == "mousedown";
                        InjectMouseButton(
                            GetInt(root, "x"), GetInt(root, "y"),
                            GetButton(GetInt(root, "button")), isMouseDown,
                            isMouseDown ? Math.Max(1, GetInt(root, "clickCount")) : 1, (CefEventFlags)(uint)GetInt(root, "mod"));
                        break;

                    case "contextMenuCommand":
                        var commandId = GetInt(root, "commandId");
                        var eventFlags = (CefEventFlags)(uint)GetInt(root, "mod");

                        if (commandId == (int)CefMenuId.ViewSource)
                        {
                            ShowViewSource();
                            CompleteContextMenu(null, default);
                        }
                        else if (commandId == (int)CefMenuId.Print)
                        {
                            ShowPrintPdf();
                            CompleteContextMenu(null, default);
                        }
                        else if (commandId == (int)CefMenuId.Copy || commandId == (int)CefMenuId.Cut)
                        {
                            CompleteContextMenu(null, default);
                            CopySelectionToClient(commandId == (int)CefMenuId.Cut);
                        }
                        else
                        {
                            CompleteContextMenu(commandId, eventFlags);
                        }
                        break;

                    case "contextMenuCancel":
                        CompleteContextMenu(null, default);
                        break;

                    case "copy":
                        CopySelectionToClient(GetString(root, "kind") == "cut");
                        break;

                    case "fileUpload":
                        CompleteFileUpload(root);
                        break;

                    case "fileUploadCancel":
                        WithSelectedSession(s => s.FileDialogHandler?.CancelFileDialog());
                        break;

                    case "wheel":
                        InjectMouseWheel(
                            GetInt(root, "x"), GetInt(root, "y"),
                            GetInt(root, "deltaX"), GetInt(root, "deltaY"),
                            (CefEventFlags)(uint)GetInt(root, "mod"));
                        break;

                    case "keydown":
                        InjectKeyDown(GetInt(root, "code"), (CefEventFlags)(uint)GetInt(root, "mod"));
                        break;

                    case "keyup":
                        InjectKeyUp(GetInt(root, "code"), (CefEventFlags)(uint)GetInt(root, "mod"));
                        break;

                    case "text":
                        InjectText(GetString(root, "value"));
                        break;

                    case "resize":
                        ResizeSelected(GetInt(root, "width"), GetInt(root, "height"), (float)GetDouble(root, "dpr"), GetString(root, "userAgent"));
                        break;

                    case "requestRepaint":
                        RequestRepaint();
                        break;

                    case "overlay":
                        SetOverlayEnabled(GetBool(root, "enabled"));
                        break;

                    case "compression":
                        SetFrameFormat(GetString(root, "format"));
                        break;

                    case "ping":
                        _frameSocketServer.BroadcastText(JsonSerializer.Serialize(new { type = "pong", t = GetDouble(root, "t") }, CamelCaseJsonOptions));
                        break;

                    case "h264quality":
                        SetH264Quality(GetString(root, "quality"));
                        break;

                    case "mobile":
                        SetMobileEmulation(GetBool(root, "enabled"), GetInt(root, "width"), GetInt(root, "height"), GetString(root, "userAgent"), GetString(root, "platform"), (float)GetDouble(root, "dpr"));
                        break;

                    case "zoom":
                        SetZoomLevel(GetDouble(root, "level"));
                        break;

                    case "back":
                        GoBackSelected();
                        break;

                    case "forward":
                        GoForwardSelected();
                        break;

                    case "reload":
                        WithSelectedBrowser(b => b.Reload(GetBool(root, "ignoreCache")));
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SessionManager] Failed to handle message: {ex}");
            }
        }

        private static string GetString(JsonElement root, string name)
            => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        private static int GetInt(JsonElement root, string name)
            => root.TryGetProperty(name, out var value) && value.TryGetInt32(out var i) ? i : 0;

        private static double GetDouble(JsonElement root, string name)
            => root.TryGetProperty(name, out var value) && value.TryGetDouble(out var d) ? d : 0;

        private static bool GetBool(JsonElement root, string name)
            => root.TryGetProperty(name, out var value) &&
               (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False) &&
               value.GetBoolean();

        private static CefMouseButtonType GetButton(int button) => button switch
        {
            1 => CefMouseButtonType.Middle,
            2 => CefMouseButtonType.Right,
            _ => CefMouseButtonType.Left,
        };

        private readonly HeadlessPopupLifeSpanHandler _popupLifeSpanHandler;

        public string AddBrowser(string url, string existingId = null, string existingContextId = null)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                url = AddressBarValue;
            }

            var id = existingId ?? Guid.NewGuid().ToString("n");

            string contextId = null;
            Func<CefRequestContext> requestContextFactory = null;
            if (_isolation != null)
            {
                contextId = IsolationContextStore.IsValidContextId(existingContextId) ? existingContextId
                    : IsolationContextStore.NewContextId();
                requestContextFactory = () => _isolation.GetOrCreate(contextId);
            }

            var browser = CreateHeadlessBrowser(requestContextFactory: requestContextFactory);
            var session = new BrowserSession(id, browser) { ContextId = contextId };

            WireSession(id, session, browser);

            _sessions[id] = session;

            Navigate(session, url);

            session.Browser.Target.InjectVisibility(false);

            LoadEndEventHandler correctSizeOnce = null;
            correctSizeOnce = (s, e) =>
            {
                browser.LoadEnd -= correctSizeOnce;
                browser.Target.Resize(browser.Target.Width, browser.Target.Height);
            };
            browser.LoadEnd += correctSizeOnce;

            if (_selectedId == null)
            {
                Select(id);
            }
            else
            {
                PushState();
            }

            return id;
        }

        internal static HeadlessCefBrowser CreateHeadlessBrowser(bool forPopup = false, int width = 1280, int height = 800,
            Func<CefRequestContext> requestContextFactory = null)
        {
            Func<CefWindowInfo> setupCefWindowInfo = () =>
            {
                var winfo = CefWindowInfo.Create();
                winfo.SharedTextureEnabled = Program.TextureEnabled;
                return winfo;
            };
            Func<CefBrowserSettings> setupCefBrowserSettings = () => new CefBrowserSettings { WindowlessFrameRate = 60 };

            return forPopup
                ? HeadlessCefBrowser.CreateForPopup(width, height, setupCefWindowInfo, setupCefBrowserSettings, host: HeadlessHost)
                : new HeadlessCefBrowser(width, height, cefRequestContextFactory: requestContextFactory,
                    setupCefWindowInfo: setupCefWindowInfo, setupCefBrowserSettings: setupCefBrowserSettings, host: HeadlessHost);
        }

        private static readonly Lazy<CefGlueHost> _headlessHost = new Lazy<CefGlueHost>(() =>
        {
            var builder = CefGlueHost.CreateBuilder();
            builder.Services.ReplaceCoordinator<IOffscreenRenderCallback, HeadlessOffscreenRenderCallback>();
            return builder.Build();
        });

        private static CefGlueHost HeadlessHost => _headlessHost.Value;

        private void WireSession(string id, BrowserSession session, HeadlessCefBrowser browser)
        {
            browser.LifeSpanHandler = _popupLifeSpanHandler;

            browser.LoadStart += (s, e) => HandleUrlChanged(session, e.Frame);
            browser.LoadEnd += (s, e) => HandleUrlChanged(session, e.Frame);

            browser.AddressChanged += (s, url) => UpdateCurrentUrl(session, url);

            browser.TitleChanged += (s, title) =>
            {
                session.Title = title ?? "";
                PushState();
            };

            browser.FaviconUrlsChanged += (s, iconUrls) =>
            {
                var iconUrl = (iconUrls != null && iconUrls.Length > 0) ? iconUrls[0] : null;
                session.FaviconUrl = "";
                PushState();

                if (iconUrl == null)
                {
                    return;
                }

                var sid = session.Id;
                Task.Run(async () =>
                {
                    var dataUri = await FaviconFetcher.TryFetchAsDataUriAsync(iconUrl);
                    if (dataUri != null && !_disposed && _sessions.TryGetValue(sid, out var s2))
                    {
                        s2.FaviconUrl = dataUri;
                        PushState();
                    }
                });
            };

            var downloadHandler = new HeadlessDownloadHandler();
            browser.DownloadHandler = downloadHandler;
            downloadHandler.DownloadCompleted += (filePath, suggestedFileName, mimeType) =>
            {
                if (session.Id != _selectedId)
                {
                    TryDeleteTempFile(filePath);
                    return;
                }

                try
                {
                    var fileBytes = File.ReadAllBytes(filePath);
                    var fileBase64 = Convert.ToBase64String(fileBytes);
                    _frameSocketServer.BroadcastText(JsonSerializer.Serialize(new
                    {
                        type = "download",
                        fileName = string.IsNullOrWhiteSpace(suggestedFileName) ? "download" : suggestedFileName,
                        mimeType = string.IsNullOrWhiteSpace(mimeType) ? "application/octet-stream" : mimeType,
                        fileBase64,
                    }));
                }
                catch (IOException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SessionManager] Download read failed: {ex}");
                }
                finally
                {
                    TryDeleteTempFile(filePath);
                }
            };
            downloadHandler.DownloadFailed += filePath => TryDeleteTempFile(filePath);

            var fileDialogHandler = new HeadlessFileDialogHandler();
            browser.DialogHandler = fileDialogHandler;
            session.FileDialogHandler = fileDialogHandler;
            fileDialogHandler.FileDialogRequested += (multiple, acceptFilters) =>
            {
                if (session.Id != _selectedId)
                {
                    fileDialogHandler.CancelFileDialog();
                    return;
                }

                _frameSocketServer.BroadcastText(JsonSerializer.Serialize(new
                {
                    type = "fileDialog",
                    multiple,
                    acceptFilters = acceptFilters ?? Array.Empty<string>(),
                }));
            };

            browser.LoadingStateChange += (s, e) =>
            {
                session.CanGoBack = e.CanGoBack;
                session.CanGoForward = e.CanGoForward;
                session.IsLoading = e.IsLoading;

                if (!e.IsLoading && session.PendingBackForwardResync)
                {
                    session.PendingBackForwardResync = false;
                    session.Browser.Reload(ignoreCache: true);
                }

                if (!_disposed)
                {
                    PushState();
                }
            };

            var renderSurface = (HeadlessRenderSurface)browser.Target.RenderSurface;
            renderSurface.FrameReady += frame => HandleFrameReady(session, frame);

            var audioSampleRate = 0;
            var audioChannels = 0;

            browser.AudioStreamStarted += (sampleRate, channels) =>
            {
                audioSampleRate = sampleRate;
                audioChannels = channels;

                var pcmuTestMode = string.Equals(Environment.GetEnvironmentVariable("CEFGLUE_WEBRTC_AUDIO_CODEC"), "pcmu", StringComparison.OrdinalIgnoreCase);
                if (pcmuTestMode)
                {
                    session.OpusEncoder?.Dispose();
                    session.OpusEncoder = null;
                    return;
                }

                try
                {
                    session.OpusEncoder?.Dispose();
                    session.OpusEncoder = new OpusAudioEncoder(sampleRate, channels, bitrateBps: Program.OpusBitrateBps);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SessionManager] OpusAudioEncoder construction failed for {sampleRate}Hz/{channels}ch, falling back to raw PCM audio: {ex.Message}");
                    session.OpusEncoder = null;
                }
            };

            browser.AudioStreamPacket += (channelData, frames, pts) =>
            {
                if (session.Id != _selectedId || audioChannels <= 0)
                {
                    return;
                }

                var opusEncoder = session.OpusEncoder;
                if (opusEncoder == null)
                {
                    _frameSocketServer.BroadcastAudio(audioSampleRate, audioChannels, channelData, frames);
                    return;
                }

                opusEncoder.Encode(channelData, frames, opusPacket =>
                {
                    _frameSocketServer.BroadcastAudioOpus(opusEncoder.EncodeSampleRate, audioChannels, opusEncoder.FrameSizeSamplesPerChannel, opusPacket);
                });
            };

            browser.CaptureAudio = true;

            browser.Target.CursorChanged += cursorType =>
            {
                if (session.Id != _selectedId)
                {
                    return;
                }

                _frameSocketServer.BroadcastText(JsonSerializer.Serialize(new { type = "cursor", cursor = cursorType.ToString() }));
            };

            browser.Target.VirtualKeyboardRequested += inputMode =>
            {
                if (session.Id != _selectedId)
                {
                    return;
                }

                var mode = inputMode switch
                {
                    CefTextInputMode.None => "none",
                    CefTextInputMode.Tel => "tel",
                    CefTextInputMode.Url => "url",
                    CefTextInputMode.Email => "email",
                    CefTextInputMode.Numeric => "numeric",
                    CefTextInputMode.Decimal => "decimal",
                    CefTextInputMode.Search => "search",
                    _ => "text",
                };
                _frameSocketServer.BroadcastText(JsonSerializer.Serialize(new { type = "keyboard", mode }));
            };

            browser.Target.ContextMenuOpened += (entries, x, y) =>
            {
                if (session.Id != _selectedId)
                {
                    return;
                }

                _frameSocketServer.BroadcastText(JsonSerializer.Serialize(new { type = "contextMenu", x, y, entries }, CamelCaseJsonOptions));
            };

            browser.Target.ContextMenuClosed += () =>
            {
                if (session.Id != _selectedId)
                {
                    return;
                }

                _frameSocketServer.BroadcastText(JsonSerializer.Serialize(new { type = "contextMenuClosed" }));
            };

            renderSurface.ShowOverlayInfo = _overlayEnabled;
        }

        internal string AdoptPopupSession(HeadlessCefBrowser browser, string initialUrl, string openerId, bool isPopup)
        {
            var id = Guid.NewGuid().ToString("n");

            var contextId = openerId != null && _sessions.TryGetValue(openerId, out var opener) ? opener.ContextId : null;

            var session = new BrowserSession(id, browser)
            {
                ContextId = contextId,
                HasNavigated = true,
                CurrentUrl = initialUrl,
                IsPopup = isPopup,
                OpenerId = openerId,
            };

            WireSession(id, session, browser);

            _sessions[id] = session;
            session.Browser.Target.InjectVisibility(false);

            LoadEndEventHandler correctSizeOnce = null;
            correctSizeOnce = (s, e) =>
            {
                browser.LoadEnd -= correctSizeOnce;
                browser.Target.Resize(browser.Target.Width, browser.Target.Height);
            };
            browser.LoadEnd += correctSizeOnce;

            Select(id);

            return id;
        }

        public void RemoveBrowser(string id)
        {
            if (id == null || !_sessions.TryGetValue(id, out var session))
            {
                return;
            }

            _sessions.Remove(id);
            session.Browser.Dispose();
            session.H264Encoder?.Dispose();
            session.OpusEncoder?.Dispose();

            if (_isolation != null && session.ContextId != null && !_sessions.Values.Any(s => s.ContextId == session.ContextId))
            {
                _isolation.Release(session.ContextId);
            }

            if (_selectedId == id)
            {
                _selectedId = null;

                var next = (session.IsPopup && session.OpenerId != null && _sessions.ContainsKey(session.OpenerId))
                    ? session.OpenerId
                    : _sessions.Keys.FirstOrDefault();

                if (next != null)
                {
                    Select(next);
                    return;
                }
            }

            PushState();
        }

        internal string FindSessionId(CefBrowser browser)
        {
            if (browser == null)
            {
                return null;
            }

            foreach (var kvp in _sessions)
            {
                if (kvp.Value.Browser.BrowserId == browser.Identifier)
                {
                    return kvp.Key;
                }
            }

            return null;
        }

        internal bool TryGetSession(string id, out BrowserSession session) => _sessions.TryGetValue(id, out session);

        internal int SessionCount => _sessions.Count;

        internal string SelectedUrl => _selectedId != null && _sessions.TryGetValue(_selectedId, out var session)
            ? session.CurrentUrl
            : null;

        internal (List<(string Id, string Url, string ContextId)> Tabs, string SelectedId) AllTabUrls
        {
            get
            {
                var tabs = _sessions.Values.Where(s => !s.IsPopup).Select(s => (s.Id, s.CurrentUrl, s.ContextId)).ToList();
                var selectedId = tabs.Any(t => t.Id == _selectedId) ? _selectedId : null;
                return (tabs, selectedId);
            }
        }

        internal void PruneIsolationContexts()
        {
            if (_isolation == null)
            {
                return;
            }

            var inUse = _sessions.Values.ToList().Select(s => s.ContextId).Where(c => c != null).ToHashSet();
            _isolation.Prune(inUse);
        }

        public void OnFrameClientConnected()
        {
            if (_selectedId != null && _sessions.TryGetValue(_selectedId, out var selected))
            {
                selected.H264Encoder?.ForceNextFrameKeyFrame();
            }

            PushState();
        }

        public void RequestRepaint()
        {
            if (_selectedId == null || !_sessions.TryGetValue(_selectedId, out var session) || !session.HasNavigated)
            {
                return;
            }

            if (!session.IsMobileEmulationActive && !string.IsNullOrEmpty(session.ResponsiveUserAgent))
            {
                session.Browser.SendDevToolsMessage("Emulation.setUserAgentOverride", new { userAgent = session.ResponsiveUserAgent });
            }

            session.H264Encoder?.ForceNextFrameKeyFrame();

            void KickWithScroll()
            {
                WithSelectedTarget(t =>
                {
                    t.InjectMouseWheel(new CefMouseEvent(5, 5, CefEventFlags.None), 0, -3);
                    t.InjectMouseWheel(new CefMouseEvent(5, 5, CefEventFlags.None), 0, 3);
                });
            }

            const string repaintKickJs =
                "(function(){var n=0," +
                "k=document.createElement('div');" +
                "k.style.cssText='position:fixed;left:0;top:0;width:1px;height:1px;pointer-events:none;z-index:-1;opacity:0';" +
                "document.documentElement.appendChild(k);" +
                "function t(){try{" +
                "k.style.opacity=(n&1)?'0.01':'0';" +
                "k.style.transform='translate('+((n&1)?1:0)+'px,0)';" +
                "k.textContent=String(n);" +
                "if(n<3){window.scrollBy(0,(n&1)?1:-1);}" +
                "}catch(e){}if(++n<32){requestAnimationFrame(t);}else{try{k.remove();}catch(e){}}}" +
                "requestAnimationFrame(t);})();";

            KickWithScroll();
            try { session.Browser.ExecuteJavaScript(repaintKickJs); } catch { }

            var sid = session.Id;
            Task.Run(async () =>
            {
                await Task.Delay(300);
                try
                {
                    if (!_disposed && _selectedId == sid && _sessions.TryGetValue(sid, out var s2))
                    {
                        KickWithScroll();
                        try { s2.Browser.ExecuteJavaScript(repaintKickJs); } catch { }
                        s2.H264Encoder?.ForceNextFrameKeyFrame();
                    }
                }
                catch { }
            });
        }

        public void Select(string id)
        {
            if (id == null || !_sessions.TryGetValue(id, out var session))
            {
                return;
            }

            if (_selectedId == id)
            {
                session.Browser.Target.InjectFocus();
                return;
            }

            if (_selectedId != null && _sessions.TryGetValue(_selectedId, out var previous))
            {
                previous.Browser.Target.InjectLostFocus();
                previous.Browser.Target.InjectVisibility(false);
            }

            _selectedId = id;
            session.Browser.Target.InjectVisibility(true);
            session.Browser.Target.InjectFocus();

            session.H264Encoder?.ForceNextFrameKeyFrame();

            PushState();
        }

        public void NavigateAll(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            AddressBarValue = url;

            foreach (var session in _sessions.Values)
            {
                if (!session.HasNavigated || session.Id == _selectedId)
                {
                    Navigate(session, url);
                }
            }

            PushState();
        }

        public void SetOverlayEnabled(bool enabled)
        {
            _overlayEnabled = enabled;

            foreach (var session in _sessions.Values)
            {
                ((HeadlessRenderSurface)session.Browser.Target.RenderSurface).ShowOverlayInfo = enabled;
            }

            PushState();
        }

        public void SetFrameFormat(string format)
        {
            _frameFormat = format switch
            {
                "png" => FramePixelFormat.Png,
                "webp" => FramePixelFormat.Webp,
                "jpg" => FramePixelFormat.Jpeg,
                "h264" => FramePixelFormat.H264,
                _ => FramePixelFormat.RawBgra,
            };

            if (_frameFormat != FramePixelFormat.H264)
            {
                WithSelectedBrowser(b => ((HeadlessRenderSurface)b.Target.RenderSurface).EncoderInfo = null);
            }

            WithSelectedBrowser(b => b.Invalidate());

            PushState();
        }

        public void SetH264Quality(string quality)
        {
            _h264Quality = quality == "best" ? VideoQuality.BestQuality : VideoQuality.Speed;

            WithSelectedBrowser(b => b.Invalidate());

            PushState();
        }

        public void SetMobileEmulation(bool enabled, int width, int height, string userAgent, string platform, float dpr = 1f)
        {
            if (_selectedId == null || !_sessions.TryGetValue(_selectedId, out var session))
            {
                return;
            }

            if (!enabled && !session.IsMobileEmulationActive)
            {
                return;
            }

            if (enabled)
            {
                if (width <= 0 || height <= 0)
                {
                    return;
                }

                if (dpr <= 0)
                {
                    dpr = 1f;
                }

                session.HasEverUsedMobileEmulation = true;

                var isLandscape = width > height;
                session.Browser.SendDevToolsMessage("Emulation.setDeviceMetricsOverride", new
                {
                    width,
                    height,
                    deviceScaleFactor = dpr,
                    mobile = true,
                    screenOrientation = new
                    {
                        type = isLandscape ? "landscapePrimary" : "portraitPrimary",
                        angle = isLandscape ? 90 : 0,
                    },
                });
                session.Browser.SendDevToolsMessage("Emulation.setTouchEmulationEnabled", new { enabled = true });
                session.Browser.SendDevToolsMessage("Emulation.setUserAgentOverride", new
                {
                    userAgent = userAgent ?? "",
                    platform = platform ?? "",
                });

                WithSelectedTarget(t =>
                {
                    t.RenderSurface.DeviceScaleFactor = dpr;
                    t.Resize(width, height);
                });
            }
            else
            {
                session.Browser.SendDevToolsMessage("Emulation.clearDeviceMetricsOverride");
                session.Browser.SendDevToolsMessage("Emulation.setTouchEmulationEnabled", new { enabled = false });
                session.Browser.SendDevToolsMessage("Emulation.setEmitTouchEventsForMouse", new { enabled = false });
                session.Browser.SendDevToolsMessage("Emulation.setUserAgentOverride", new { userAgent = "" });
            }

            session.IsMobileEmulationActive = enabled;
            session.ResponsiveUserAgent = null;
            session.Browser.Reload(ignoreCache: true);
        }

        public void SetZoomLevel(double level)
        {
            WithSelectedBrowser(b => b.ZoomLevel = level);
            PushState();
        }

        public void GoBackSelected()
        {
            if (_selectedId == null || !_sessions.TryGetValue(_selectedId, out var session))
            {
                return;
            }

            if (session.HasEverUsedMobileEmulation)
            {
                session.PendingBackForwardResync = true;
            }

            session.Browser.GoBack();
        }

        public void GoForwardSelected()
        {
            if (_selectedId == null || !_sessions.TryGetValue(_selectedId, out var session))
            {
                return;
            }

            if (session.HasEverUsedMobileEmulation)
            {
                session.PendingBackForwardResync = true;
            }

            session.Browser.GoForward();
        }

        public void InjectMouseMove(int x, int y, CefEventFlags modifiers)
        {
            WithSelectedTarget(t => t.InjectMouseMove(new CefMouseEvent(x, y, modifiers)));
        }

        public void InjectMouseButton(int x, int y, CefMouseButtonType button, bool isDown, int clickCount, CefEventFlags modifiers)
        {
            WithSelectedTarget(t =>
            {
                var mouseEvent = new CefMouseEvent(x, y, modifiers);

                if (isDown)
                {
                    t.InjectMouseButtonDown(mouseEvent, button, clickCount);
                }
                else
                {
                    t.InjectMouseButtonUp(mouseEvent, button);
                }
            });
        }

        public void CompleteContextMenu(int? commandId, CefEventFlags modifiers)
        {
            WithSelectedTarget(t => t.CompleteContextMenu(commandId, modifiers));
        }

        private void CompleteFileUpload(JsonElement root)
        {
            if (_selectedId == null || !_sessions.TryGetValue(_selectedId, out var session) || session.FileDialogHandler == null)
            {
                return;
            }

            var tempPaths = new List<string>();

            if (root.TryGetProperty("files", out var filesElement) && filesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var fileElement in filesElement.EnumerateArray())
                {
                    var fileName = GetString(fileElement, "fileName");
                    var fileBase64 = GetString(fileElement, "fileBase64");

                    if (string.IsNullOrEmpty(fileBase64))
                    {
                        continue;
                    }

                    try
                    {
                        var safeName = string.IsNullOrWhiteSpace(fileName) ? "upload" : Path.GetFileName(fileName);
                        if (string.IsNullOrWhiteSpace(safeName))
                        {
                            safeName = "upload";
                        }

                        var tempDir = Path.Combine(Path.GetTempPath(), $"cefglue-upload-{Guid.NewGuid():N}");
                        Directory.CreateDirectory(tempDir);
                        var tempPath = Path.Combine(tempDir, safeName);
                        File.WriteAllBytes(tempPath, Convert.FromBase64String(fileBase64));
                        tempPaths.Add(tempPath);

                        var cleanupPath = tempPath;
                        var cleanupDir = tempDir;
                        _ = Task.Delay(TimeSpan.FromMinutes(10)).ContinueWith(_ =>
                        {
                            TryDeleteTempFile(cleanupPath);
                            try { Directory.Delete(cleanupDir); }
                            catch (IOException) { }
                            catch (UnauthorizedAccessException) { }
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SessionManager] Failed to write uploaded file '{fileName}': {ex}");
                    }
                }
            }

            session.FileDialogHandler.CompleteFileDialog(tempPaths.ToArray());
        }

        private void ShowViewSource()
        {
            if (_selectedId == null || !_sessions.TryGetValue(_selectedId, out var session))
            {
                return;
            }

            var currentUrl = session.CurrentUrl;
            if (string.IsNullOrWhiteSpace(currentUrl))
            {
                return;
            }

            var newId = AddBrowser("view-source:" + currentUrl);
            Select(newId);
        }

        private void GetSourceIntoClientModal()
        {
            if (_selectedId == null || !_sessions.TryGetValue(_selectedId, out var session))
            {
                return;
            }

            var frame = session.Browser.GetMainFrame();
            if (frame == null)
            {
                return;
            }

            var sessionIdAtRequestTime = _selectedId;
            frame.GetSource(new ActionStringVisitor(html =>
            {
                if (_disposed || sessionIdAtRequestTime != _selectedId)
                {
                    return;
                }

                _frameSocketServer.BroadcastText(JsonSerializer.Serialize(new { type = "viewSource", html }));
            }));
        }

        private const string SelectionScript =
            "var a=document.activeElement;" +
            "if(a&&(a.tagName==='TEXTAREA'||(a.tagName==='INPUT'&&/^(text|search|url|tel|email|number)?$/i.test(a.type)))" +
            "&&typeof a.selectionStart==='number'&&a.selectionEnd>a.selectionStart)" +
            "{return a.value.substring(a.selectionStart,a.selectionEnd);}" +
            "return window.getSelection?String(window.getSelection()):'';";

        private async void CopySelectionToClient(bool cut)
        {
            if (_selectedId == null || !_sessions.TryGetValue(_selectedId, out var session))
            {
                return;
            }

            var frame = session.Browser.GetFocusedFrame() ?? session.Browser.GetMainFrame();
            if (frame == null)
            {
                return;
            }

            var sessionIdAtRequestTime = _selectedId;
            string text = null;
            try
            {
                text = await session.Browser.EvaluateJavaScript<string>(SelectionScript, frame, timeout: TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Clipboard] Reading the selection failed: {ex.Message}");
            }

            if (cut)
            {
                frame.Cut();
            }
            else
            {
                frame.Copy();
            }

            Console.WriteLine($"[Clipboard] {(cut ? "cut" : "copy")}: {text?.Length ?? 0} chars from the page selection");

            if (_disposed || sessionIdAtRequestTime != _selectedId || string.IsNullOrEmpty(text))
            {
                return;
            }

            _frameSocketServer.BroadcastText(JsonSerializer.Serialize(new { type = "clipboard", text }));
        }

        private sealed class ActionStringVisitor : CefStringVisitor
        {
            private readonly Action<string> _onVisit;

            public ActionStringVisitor(Action<string> onVisit)
            {
                _onVisit = onVisit;
            }

            protected override void Visit(string value) => _onVisit(value);
        }

        private void ShowPrintPdf()
        {
            if (_selectedId == null || !_sessions.TryGetValue(_selectedId, out var session))
            {
                return;
            }

            var host = session.Browser.GetBrowserHost();
            if (host == null)
            {
                return;
            }

            var sessionIdAtRequestTime = _selectedId;
            var tempPath = Path.Combine(Path.GetTempPath(), $"cefglue-print-{Guid.NewGuid():N}.pdf");

            host.PrintToPdf(tempPath, new CefPdfPrintSettings { PrintBackground = true }, new ActionPdfPrintCallback((path, ok) =>
            {
                try
                {
                    if (_disposed || sessionIdAtRequestTime != _selectedId)
                    {
                        return;
                    }

                    if (!ok)
                    {
                        System.Diagnostics.Debug.WriteLine("[SessionManager] PrintToPdf reported failure.");
                        return;
                    }

                    var pdfBytes = File.ReadAllBytes(path);
                    var pdfBase64 = Convert.ToBase64String(pdfBytes);
                    _frameSocketServer.BroadcastText(JsonSerializer.Serialize(new { type = "printPdf", pdfBase64 }));
                }
                finally
                {
                    TryDeleteTempFile(path);
                }
            }));
        }

        private static void TryDeleteTempFile(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private sealed class ActionPdfPrintCallback : CefPdfPrintCallback
        {
            private readonly Action<string, bool> _onFinished;

            public ActionPdfPrintCallback(Action<string, bool> onFinished)
            {
                _onFinished = onFinished;
            }

            protected override void OnPdfPrintFinished(string path, bool ok) => _onFinished(path, ok);
        }

        public void InjectMouseWheel(int x, int y, int deltaX, int deltaY, CefEventFlags modifiers)
        {
            WithSelectedTarget(t => t.InjectMouseWheel(new CefMouseEvent(x, y, modifiers), deltaX, deltaY));
        }

        public void InjectKeyDown(int windowsKeyCode, CefEventFlags modifiers)
        {
            WithSelectedTarget(t => t.InjectKeyDown(new CefKeyEvent
            {
                EventType = CefKeyEventType.RawKeyDown,
                WindowsKeyCode = windowsKeyCode,
                Modifiers = modifiers,
            }, out _));
        }

        public void InjectKeyUp(int windowsKeyCode, CefEventFlags modifiers)
        {
            WithSelectedTarget(t => t.InjectKeyUp(new CefKeyEvent
            {
                EventType = CefKeyEventType.KeyUp,
                WindowsKeyCode = windowsKeyCode,
                Modifiers = modifiers,
            }, out _));
        }

        public void InjectText(string text)
        {
            if (text == null)
            {
                return;
            }

            WithSelectedTarget(t => t.InjectText(text, out _));
        }

        public void ResizeSelected(int width, int height, float dpr = 1f, string userAgent = null)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

            if (dpr <= 0)
            {
                dpr = 1f;
            }

            if (_selectedId != null && _sessions.TryGetValue(_selectedId, out var session))
            {
                if (!string.IsNullOrEmpty(userAgent) && !session.IsMobileEmulationActive)
                {
                    session.Browser.SendDevToolsMessage("Emulation.setUserAgentOverride", new { userAgent });

                    session.Browser.SendDevToolsMessage("Emulation.setTouchEmulationEnabled", new { enabled = true, maxTouchPoints = 5 });
                    session.Browser.SendDevToolsMessage("Emulation.setEmitTouchEventsForMouse", new { enabled = true, configuration = "mobile" });

                    if (userAgent != session.ResponsiveUserAgent)
                    {
                        session.ResponsiveUserAgent = userAgent;
                        session.Browser.Reload(ignoreCache: true);
                    }
                }
            }

            WithSelectedTarget(t =>
            {
                t.RenderSurface.DeviceScaleFactor = dpr;
                t.Resize(width, height);
            });
        }

        private void WithSelectedTarget(Action<HeadlessTarget> action)
        {
            if (_selectedId != null && _sessions.TryGetValue(_selectedId, out var session))
            {
                action(session.Browser.Target);
            }
        }

        private void WithSelectedBrowser(Action<HeadlessCefBrowser> action)
        {
            if (_selectedId != null && _sessions.TryGetValue(_selectedId, out var session))
            {
                action(session.Browser);
            }
        }

        private void WithSelectedSession(Action<BrowserSession> action)
        {
            if (_selectedId != null && _sessions.TryGetValue(_selectedId, out var session))
            {
                action(session);
            }
        }

        private void Navigate(BrowserSession session, string url)
        {
            session.Browser.Address = url;
            session.HasNavigated = true;

            if (!IsScriptUrl(url))
            {
                session.CurrentUrl = url;
            }
        }

        internal static bool IsScriptUrl(string url) =>
            url != null && url.TrimStart().StartsWith("javascript:", StringComparison.OrdinalIgnoreCase);

        private void HandleUrlChanged(BrowserSession session, CefFrame frame)
        {
            if (!frame.IsMain)
            {
                return;
            }

            UpdateCurrentUrl(session, frame.Url);
        }

        private void UpdateCurrentUrl(BrowserSession session, string url)
        {
            if (_disposed)
            {
                return;
            }

            session.CurrentUrl = url;
            PushState();
        }

        private void HandleFrameReady(BrowserSession session, HeadlessFrame frame)
        {
            if (_disposed || session.Id != _selectedId)
            {
                return;
            }

            if (_frameFormat == FramePixelFormat.RawBgra)
            {
                _frameSocketServer.Broadcast(frame);
                return;
            }

            lock (_encodeLock)
            {
                _pendingEncodeFrame = frame;
                _pendingEncodeSessionId = session.Id;

                if (_isEncoding)
                {
                    return;
                }

                _isEncoding = true;
            }

            Task.Run(RunEncodeLoop);
        }

        private readonly object _encodeLock = new object();
        private HeadlessFrame _pendingEncodeFrame;
        private string _pendingEncodeSessionId;
        private bool _isEncoding;

        private void RunEncodeLoop()
        {
            while (true)
            {
                HeadlessFrame frame;
                FramePixelFormat format;
                string sessionId;

                lock (_encodeLock)
                {
                    frame = _pendingEncodeFrame;
                    sessionId = _pendingEncodeSessionId;
                    _pendingEncodeFrame = null;

                    if (frame == null)
                    {
                        _isEncoding = false;
                        return;
                    }

                    format = _frameFormat;
                }

                if (_disposed || sessionId != _selectedId)
                {
                    continue;
                }

                try
                {
                    if (format == FramePixelFormat.Webp)
                    {
                        var webpBytes = WebPFrameEncoder.Encode(frame.Buffer, frame.Width, frame.Height, quality: 80);
                        _frameSocketServer.BroadcastCompressed(webpBytes, frame.Width, frame.Height, FramePixelFormat.Webp);
                    }
                    else if (format == FramePixelFormat.Png)
                    {
                        var pngBytes = PngFrameEncoder.Encode(frame.Buffer, frame.Width, frame.Height);
                        _frameSocketServer.BroadcastCompressed(pngBytes, frame.Width, frame.Height, FramePixelFormat.Png);
                    }
                    else if (format == FramePixelFormat.Jpeg)
                    {
                        var jpegBytes = JpegFrameEncoder.Encode(frame.Buffer, frame.Width, frame.Height, quality:60);
                        _frameSocketServer.BroadcastCompressed(jpegBytes, frame.Width, frame.Height, FramePixelFormat.Jpeg);
                    }
                    else if (format == FramePixelFormat.H264)
                    {
                        EncodeAndBroadcastH264(sessionId, frame);
                    }
                    else
                    {
                        _frameSocketServer.Broadcast(frame);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[SessionManager] RunEncodeLoop: encode failed for session {sessionId} (format {format}), dropping this frame: {ex}");
                }
            }
        }

        private void EncodeAndBroadcastH264(string sessionId, HeadlessFrame frame)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                return;
            }

            if (_h264Quality == VideoQuality.Speed && frame.DirtyRects != null && frame.DirtyRects.Length == 0)
            {
                return;
            }

            var usesScalableEncoder = false;
            var targetEncodeWidth = usesScalableEncoder ? (int)(frame.Width * _h264ResolutionScale) & ~1 : frame.Width;
            var targetEncodeHeight = usesScalableEncoder ? (int)(frame.Height * _h264ResolutionScale) & ~1 : frame.Height;

            if (session.H264Encoder == null
                || session.H264EncoderWidth != targetEncodeWidth
                || session.H264EncoderHeight != targetEncodeHeight
                || session.H264EncoderQuality != _h264Quality)
            {
                try
                {
                session.H264Encoder?.Dispose();

                var encoderPlugin = Program.SelectedEncoderPlugin
                    ?? throw new InvalidOperationException("no video encoder plugin is installed (plugins/encoders is empty)");
                session.H264Encoder = encoderPlugin.Create(new EncoderCreateArgs
                {
                    Width = frame.Width,
                    Height = frame.Height,
                    Quality = _h264Quality,
                    EncodeWidth = targetEncodeWidth,
                    EncodeHeight = targetEncodeHeight,
                    Settings = Program.EncoderPluginSettings,
                });
                session.H264EncoderWidth = targetEncodeWidth;
                session.H264EncoderHeight = targetEncodeHeight;
                session.H264EncoderQuality = _h264Quality;
                session.H264EncoderGeneration = ++_nextH264EncoderGeneration;
                session.LastEncoderErrorKey = null;

                var encoderName = encoderPlugin.HudName;
                ((HeadlessRenderSurface)session.Browser.Target.RenderSurface).EncoderInfo = encoderName;

                session.H264Encoder.CodecStringCorrected += PushState;

                PushState();
                }
                catch (Exception ex)
                {
                    var encoderLabel = Program.SelectedEncoderPlugin?.Id ?? "none";
                    System.Diagnostics.Debug.WriteLine(
                        $"[SessionManager] Encoder construction failed for {targetEncodeWidth}x{targetEncodeHeight} " +
                        $"(quality={_h264Quality}, encoder={encoderLabel}): {ex.Message}");

                    var errorKey = $"{targetEncodeWidth}x{targetEncodeHeight}:{_h264Quality}:{encoderLabel}";
                    if (session.LastEncoderErrorKey != errorKey)
                    {
                        session.LastEncoderErrorKey = errorKey;
                        _frameSocketServer.BroadcastText(JsonSerializer.Serialize(new
                        {
                            type = "encoderError",
                            encoder = encoderLabel,
                            message = $"Video encoder '{encoderLabel}' isn't supported on this machine ({ex.Message}). Video is paused until this is fixed (e.g. pick a different CEFGLUE_VIDEO_ENCODER) or the resolution changes.",
                        }));
                    }

                    session.H264Encoder = null;
                    session.H264EncoderWidth = -1;
                    session.H264EncoderHeight = -1;
                    return;
                }
            }

            var generation = session.H264EncoderGeneration;

            var encodeStopwatch = usesScalableEncoder ? System.Diagnostics.Stopwatch.StartNew() : null;

            session.H264Encoder.Encode(frame.Buffer, (packetBytes, isKeyFrame) =>
            {
                _frameSocketServer.BroadcastCompressed(packetBytes, frame.Width, frame.Height, FramePixelFormat.H264, isKeyFrame, generation);
            });

            if (usesScalableEncoder)
            {
                encodeStopwatch.Stop();
                UpdateH264AdaptiveScale(encodeStopwatch.Elapsed.TotalMilliseconds);
            }
        }

        private double _h264ResolutionScale = 1.0;
        private double _h264EncodeUsageEma = -1;
        private int _h264ConsecutiveOveruse;
        private int _h264ConsecutiveUnderuse;
        private DateTime _h264LastScaleChange = DateTime.MinValue;

        private const double H264OveruseThreshold = 85.0;
        private const double H264UnderuseThreshold = 42.0;
        private const double H264TargetFrameIntervalMs = 1000.0 / 30;
        private const double H264EmaAlpha = 0.15;
        private const int H264MinConsecutiveForDown = 60;
        private const int H264MinConsecutiveForUp = 150;
        private static readonly TimeSpan H264ScaleChangeCooldown = TimeSpan.FromSeconds(3);
        private const double H264ScaleStepDown = 0.75;
        private const double H264MinResolutionScale = 0.5;

        private void UpdateH264AdaptiveScale(double lastEncodeMs)
        {
            var usagePercent = lastEncodeMs / H264TargetFrameIntervalMs * 100.0;
            _h264EncodeUsageEma = _h264EncodeUsageEma < 0
                ? usagePercent
                : (_h264EncodeUsageEma * (1 - H264EmaAlpha)) + (usagePercent * H264EmaAlpha);

            if (_h264EncodeUsageEma > H264OveruseThreshold)
            {
                _h264ConsecutiveOveruse++;
                _h264ConsecutiveUnderuse = 0;
            }
            else if (_h264EncodeUsageEma < H264UnderuseThreshold)
            {
                _h264ConsecutiveUnderuse++;
                _h264ConsecutiveOveruse = 0;
            }
            else
            {
                _h264ConsecutiveOveruse = 0;
                _h264ConsecutiveUnderuse = 0;
            }

            var now = DateTime.UtcNow;
            if (now - _h264LastScaleChange < H264ScaleChangeCooldown)
            {
                return;
            }

            if (_h264ConsecutiveOveruse >= H264MinConsecutiveForDown && _h264ResolutionScale > H264MinResolutionScale)
            {
                _h264ResolutionScale = Math.Max(H264MinResolutionScale, _h264ResolutionScale * H264ScaleStepDown);
                _h264LastScaleChange = now;
                _h264ConsecutiveOveruse = 0;
                System.Diagnostics.Debug.WriteLine($"[H264 adaptive] SUSTAINED OVERUSE (ema={_h264EncodeUsageEma:F0}%) - scaling DOWN to {_h264ResolutionScale:F2}x");
            }
            else if (_h264ConsecutiveUnderuse >= H264MinConsecutiveForUp && _h264ResolutionScale < 1.0)
            {
                _h264ResolutionScale = Math.Min(1.0, _h264ResolutionScale / H264ScaleStepDown);
                _h264LastScaleChange = now;
                _h264ConsecutiveUnderuse = 0;
                System.Diagnostics.Debug.WriteLine($"[H264 adaptive] SUSTAINED UNDERUSE (ema={_h264EncodeUsageEma:F0}%) - scaling UP to {_h264ResolutionScale:F2}x");
            }
        }

        private void PushState()
        {
            if (_disposed)
            {
                return;
            }

            var state = new
            {
                type = "state",
                browsers = _sessions.Values
                    .Select(s => new
                    {
                        id = s.Id,
                        url = s.CurrentUrl,
                        title = s.Title,
                        faviconUrl = s.FaviconUrl,
                        contextId = s.ContextId,
                        contextIndex = _isolation?.GetIndex(s.ContextId),
                        isPopup = s.IsPopup,
                        canGoBack = s.CanGoBack,
                        canGoForward = s.CanGoForward,
                        isLoading = s.IsLoading,
                    })
                    .ToArray(),
                selectedId = _selectedId,
                addressBarValue = AddressBarValue,
                overlayEnabled = _overlayEnabled,
                frameFormat = _frameFormat switch
                {
                    FramePixelFormat.Png => "png",
                    FramePixelFormat.Webp => "webp",
                    FramePixelFormat.Jpeg => "jpg",
                    FramePixelFormat.H264 => "h264",
                    _ => "raw",
                },
                h264Quality = _h264Quality == VideoQuality.BestQuality ? "best" : "speed",
                h264CodecString = _sessions.TryGetValue(_selectedId ?? string.Empty, out var selectedSession)
                    ? selectedSession.H264Encoder?.CodecString
                    : null,
                zoomLevel = selectedSession?.Browser.ZoomLevel,
            };

            _frameSocketServer.BroadcastText(JsonSerializer.Serialize(state));
        }

        public void Dispose()
        {
            _disposed = true;

            foreach (var session in _sessions.Values)
            {
                session.Browser.Dispose();
                session.H264Encoder?.Dispose();
                session.OpusEncoder?.Dispose();
            }

            _sessions.Clear();
        }
    }
}
