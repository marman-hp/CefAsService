using System;

namespace Xilium.CefGlue.Headless.Server
{
    public sealed class RecordingFile
    {
        public string Path { get; init; }

        public string FileName { get; init; }

        public string ContentType { get; init; } = "video/mp4";

        public long BytesPerSecond { get; init; }

        public Action Delivered { get; init; }
    }
}
