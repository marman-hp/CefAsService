namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefAudioHandler
    {
        private int get_audio_parameters(cef_audio_handler_t* self, cef_browser_t* browser, cef_audio_parameters_t* @params)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);

            var mResult = GetAudioParameters(mBrowser, new CefAudioParameters(@params));
            return mResult ? 1 : 0;
        }

        protected abstract bool GetAudioParameters(CefBrowser browser, CefAudioParameters parameters);

        private void on_audio_stream_started(cef_audio_handler_t* self, cef_browser_t* browser, cef_audio_parameters_t* @params, int channels)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            OnAudioStreamStarted(mBrowser, new CefAudioParameters(@params), channels);
        }

        protected abstract void OnAudioStreamStarted(CefBrowser browser, in CefAudioParameters parameters, int channels);

        private void on_audio_stream_packet(cef_audio_handler_t* self, cef_browser_t* browser, float** data, int frames, long pts)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            OnAudioStreamPacket(mBrowser, (IntPtr)data, frames, pts);
        }

        protected abstract void OnAudioStreamPacket(CefBrowser browser, IntPtr data, int frames, long pts);

        private void on_audio_stream_stopped(cef_audio_handler_t* self, cef_browser_t* browser)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            OnAudioStreamStopped(mBrowser);
        }

        protected abstract void OnAudioStreamStopped(CefBrowser browser);

        private void on_audio_stream_error(cef_audio_handler_t* self, cef_browser_t* browser, cef_string_t* message)
        {
            CheckSelf(self);

            var mBrowser = CefBrowser.FromNative(browser);
            var mMessage = cef_string_t.ToString(message);
            OnAudioStreamError(mBrowser, mMessage);
        }

        protected abstract void OnAudioStreamError(CefBrowser browser, string message);
    }
}
