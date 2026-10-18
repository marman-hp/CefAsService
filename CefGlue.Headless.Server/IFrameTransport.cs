using System;
using Xilium.CefGlue.Headless;

namespace Xilium.CefGlue.Headless.Server
{
    public interface IFrameTransport
    {
        event Action<string> MessageReceived;

        event Action Connected;
        event Action Disconnected;

        void Broadcast(HeadlessFrame frame);
        void BroadcastCompressed(byte[] encodedBytes, int width, int height, FramePixelFormat format, bool isKeyFrame = false, int generation = 0);
        void BroadcastAudio(int sampleRate, int channels, float[][] channelData, int frames);
        void BroadcastAudioOpus(int sampleRate, int channels, int frames, byte[] opusPacket);
        void BroadcastText(string text);
    }
}
