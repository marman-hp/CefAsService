using System;

namespace Xilium.CefGlue.Headless
{
    public interface IVideoFrameEncoder : IDisposable
    {
        string CodecString { get; }

        event Action CodecStringCorrected;

        void Encode(byte[] bgraBuffer, Action<byte[], bool> onPacket);

        void ForceNextFrameKeyFrame();
    }
}
