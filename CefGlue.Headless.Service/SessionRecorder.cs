using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Xilium.CefGlue.Headless.Service
{
    internal sealed unsafe partial class SessionRecorder
    {
        public const int Ok = 0, BlockedPaused = 1, Resumed = 2, Dropped = 3, Stopped = 4;
        public const int BadArgument = -1, BadHandle = -2, Blocked = -3, NoConsent = -4, IoError = -5, Busy = -6, BufferTooSmall = -7;

        private const string Lib = "cefrec";

        [LibraryImport(Lib, EntryPoint = "rec_begin")] private static partial long RecBegin(byte* json, byte* err, int errCap);
        [LibraryImport(Lib, EntryPoint = "rec_video")] private static partial int RecVideo(long handle, byte* data, int len, int isKey, int width, int height);
        [LibraryImport(Lib, EntryPoint = "rec_audio")] private static partial int RecAudio(long handle, byte* data, int len, int sampleRate, int channels, int frameSize);
        [LibraryImport(Lib, EntryPoint = "rec_audio_pcm")] private static partial int RecAudioPcm(long handle, float* interleaved, int frames, int channels, int sampleRate);
        [LibraryImport(Lib, EntryPoint = "rec_pause")] private static partial int RecPause(long handle);
        [LibraryImport(Lib, EntryPoint = "rec_resume")] private static partial int RecResume(long handle);
        [LibraryImport(Lib, EntryPoint = "rec_navigated")] private static partial int RecNavigated(long handle, byte* url, byte* title);
        [LibraryImport(Lib, EntryPoint = "rec_status")] private static partial int RecStatus(long handle, byte* buf, int cap);
        [LibraryImport(Lib, EntryPoint = "rec_end")] private static partial int RecEnd(long handle, byte* buf, int cap);

        private long _handle;
        private int _stopSignalled;

        public string TabId { get; }
        public bool IncludeCursor { get; }

        public string Kind { get; }

        public bool IsVideo => Kind != "audio";

        private float[] _pcm = Array.Empty<float>();

        public bool BlockedNotified { get; set; }

        private SessionRecorder(string tabId, long handle, bool includeCursor, string kind)
        {
            TabId = tabId;
            _handle = handle;
            IncludeCursor = includeCursor;
            Kind = kind == "audio" ? "audio" : "video";
        }

        public static SessionRecorder TryStart(string tabId, string kind, object config, bool includeCursor, out int code, out string error)
        {
            try
            {
                var json = Utf8Z(JsonSerializer.Serialize(config));
                var err = new byte[1024];
                long handle;
                fixed (byte* j = json)
                fixed (byte* e = err)
                {
                    handle = RecBegin(j, e, err.Length);
                }

                if (handle > 0)
                {
                    code = Ok;
                    error = null;
                    return new SessionRecorder(tabId, handle, includeCursor, kind);
                }

                code = (int)handle;
                error = FromUtf8Z(err);
                if (string.IsNullOrEmpty(error)) error = $"Recording could not start (code {code})";
                return null;
            }
            catch (DllNotFoundException)
            {
                code = BadArgument;
                error = "Recording component (cefrec.dll) is not installed";
                return null;
            }
            catch (EntryPointNotFoundException)
            {
                code = BadArgument;
                error = "Recording component (cefrec.dll) is outdated";
                return null;
            }
        }

        public int Video(byte[] packet, bool isKey, int width, int height)
        {
            var h = Interlocked.Read(ref _handle);
            if (h == 0 || packet == null || packet.Length == 0) return Dropped;
            fixed (byte* p = packet) return RecVideo(h, p, packet.Length, isKey ? 1 : 0, width, height);
        }

        public int Audio(byte[] packet, int sampleRate, int channels, int frameSize)
        {
            var h = Interlocked.Read(ref _handle);
            if (h == 0 || packet == null || packet.Length == 0) return Dropped;
            fixed (byte* p = packet) return RecAudio(h, p, packet.Length, sampleRate, channels, frameSize);
        }

        public int AudioPcm(float[][] planar, int frames, int channels, int sampleRate)
        {
            var h = Interlocked.Read(ref _handle);
            if (h == 0 || planar == null || frames <= 0 || channels <= 0 || planar.Length < channels) return Dropped;

            var needed = frames * channels;
            if (_pcm.Length < needed) _pcm = new float[needed];
            for (var c = 0; c < channels; c++)
            {
                var src = planar[c];
                for (var i = 0; i < frames; i++)
                {
                    _pcm[i * channels + c] = src[i];
                }
            }

            fixed (float* p = _pcm) return RecAudioPcm(h, p, frames, channels, sampleRate);
        }

        public int Pause() => Interlocked.Read(ref _handle) is var h and not 0 ? RecPause(h) : BadHandle;

        public int Resume() => Interlocked.Read(ref _handle) is var h and not 0 ? RecResume(h) : BadHandle;

        public int Navigated(string url, string title)
        {
            var h = Interlocked.Read(ref _handle);
            if (h == 0) return BadHandle;
            var u = Utf8Z(url ?? "");
            var t = Utf8Z(title ?? "");
            fixed (byte* up = u)
            fixed (byte* tp = t)
            {
                return RecNavigated(h, up, tp);
            }
        }

        public JsonDocument Status()
        {
            var h = Interlocked.Read(ref _handle);
            return h == 0 ? null : ReadJson(h, RecStatusThunk);
        }

        public bool SignalStopped() => Interlocked.Exchange(ref _stopSignalled, 1) == 0;

        public JsonDocument End()
        {
            var h = Interlocked.Exchange(ref _handle, 0);
            if (h == 0) return null;

            for (var attempt = 0; attempt < 30; attempt++)
            {
                var result = ReadJson(h, RecEndThunk, out var code);
                if (code != Busy) return result;
            }

            return null;
        }

        private delegate int JsonCall(long handle, byte* buf, int cap);

        private static int RecStatusThunk(long handle, byte* buf, int cap) => RecStatus(handle, buf, cap);

        private static int RecEndThunk(long handle, byte* buf, int cap) => RecEnd(handle, buf, cap);

        private static JsonDocument ReadJson(long handle, JsonCall call) => ReadJson(handle, call, out _);

        private static JsonDocument ReadJson(long handle, JsonCall call, out int code)
        {
            for (var cap = 16 * 1024; cap <= 4 * 1024 * 1024; cap *= 4)
            {
                var buf = new byte[cap];
                fixed (byte* b = buf)
                {
                    code = call(handle, b, cap);
                }

                if (code == BufferTooSmall) continue;
                if (code < 0) return null;
                return JsonDocument.Parse(Encoding.UTF8.GetString(buf, 0, code));
            }

            code = BufferTooSmall;
            return null;
        }

        private static byte[] Utf8Z(string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            Array.Resize(ref bytes, bytes.Length + 1);
            return bytes;
        }

        private static string FromUtf8Z(byte[] buf)
        {
            var n = Array.IndexOf(buf, (byte)0);
            return Encoding.UTF8.GetString(buf, 0, n < 0 ? buf.Length : n);
        }
    }
}
