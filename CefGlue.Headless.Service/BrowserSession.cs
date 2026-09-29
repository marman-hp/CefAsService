using System;
using Xilium.CefGlue.Headless;

namespace Xilium.CefGlue.Headless.Service
{
    internal sealed class BrowserSession
    {
        public string Id { get; }
        public HeadlessCefBrowser Browser { get; }

        public bool HasNavigated { get; set; }

        public string CurrentUrl { get; set; } = "";

        public string Title { get; set; } = "";

        public string FaviconUrl { get; set; } = "";

        public HeadlessFileDialogHandler FileDialogHandler { get; set; }

        public bool IsMobileEmulationActive { get; set; }

        public string ResponsiveUserAgent { get; set; }

        public bool HasEverUsedMobileEmulation { get; set; }

        public bool PendingBackForwardResync { get; set; }

        public bool CanGoBack { get; set; }
        public bool CanGoForward { get; set; }
        public bool IsLoading { get; set; }

        public bool IsPopup { get; init; }

        public string OpenerId { get; init; }

        public string ContextId { get; init; }

        public IVideoFrameEncoder H264Encoder { get; set; }
        public int H264EncoderWidth { get; set; }
        public int H264EncoderHeight { get; set; }
        public VideoQuality H264EncoderQuality { get; set; }

        public OpusAudioEncoder OpusEncoder { get; set; }

        public int H264EncoderGeneration { get; set; }

        public string LastEncoderErrorKey { get; set; }

        public BrowserSession(string id, HeadlessCefBrowser browser)
        {
            Id = id;
            Browser = browser;
        }
    }
}
