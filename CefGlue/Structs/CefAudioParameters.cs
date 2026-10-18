using System;
using Xilium.CefGlue.Interop;

namespace Xilium.CefGlue
{
    public unsafe ref struct CefAudioParameters
    {
        private readonly cef_audio_parameters_t* _target;

        internal CefAudioParameters(cef_audio_parameters_t* target)
        {
            _target = target;
        }

        public CefChannelLayout ChannelLayout
        {
            readonly get { CheckSelf(); return _target->channel_layout; }
            set { CheckSelf(); _target->channel_layout = value; }
        }

        public int SampleRate
        {
            readonly get { CheckSelf(); return _target->sample_rate; }
            set { CheckSelf(); _target->sample_rate = value; }
        }

        public int FramesPerBuffer
        {
            readonly get { CheckSelf(); return _target->frames_per_buffer; }
            set { CheckSelf(); _target->frames_per_buffer = value; }
        }

        private readonly void CheckSelf()
        {
            if (_target == null) ThrowCheckSelfFailed();
        }

        private static void ThrowCheckSelfFailed()
        {
            throw new InvalidOperationException("CefAudioParameters is null.");
        }
    }
}
