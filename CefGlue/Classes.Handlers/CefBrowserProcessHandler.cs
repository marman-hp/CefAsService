namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefBrowserProcessHandler
    {
        private void on_register_custom_preferences(cef_browser_process_handler_t* self, CefPreferencesType type, cef_preference_registrar_t* registrar)
        {
            CheckSelf(self);

            var mRegistrar = CefPreferenceRegistrar.FromNative(registrar);

            try
            {
                OnRegisterCustomPreferences(type, mRegistrar);
            }
            finally
            {
                mRegistrar.ReleaseObject();
            }
        }

        protected virtual void OnRegisterCustomPreferences(CefPreferencesType type, CefPreferenceRegistrar registrar)
        { }

        private void on_context_initialized(cef_browser_process_handler_t* self)
        {
            CheckSelf(self);

            OnContextInitialized();
        }

        protected virtual void OnContextInitialized()
        {
        }

        private void on_before_child_process_launch(cef_browser_process_handler_t* self, cef_command_line_t* command_line)
        {
            CheckSelf(self);

            using (var m_commandLine = CefCommandLine.FromNative(command_line))
            {
                OnBeforeChildProcessLaunch(m_commandLine);
            }
        }

        protected virtual void OnBeforeChildProcessLaunch(CefCommandLine commandLine)
        {
        }

        private int on_already_running_app_relaunch(cef_browser_process_handler_t* self, cef_command_line_t* command_line, cef_string_t* current_directory)
        {
            CheckSelf(self);

            using (var m_commandLine = CefCommandLine.FromNative(command_line))
            {
                return OnAlreadyRunningAppRelaunch(m_commandLine, cef_string_t.ToString(current_directory)) ? 1 : 0;
            }
        }

        protected virtual bool OnAlreadyRunningAppRelaunch(CefCommandLine commandLine, string currentDirectory)
        {
            return false;
        }

        private void on_schedule_message_pump_work(cef_browser_process_handler_t* self, long delay_ms)
        {
            CheckSelf(self);
            OnScheduleMessagePumpWork(delay_ms);
        }

        protected virtual void OnScheduleMessagePumpWork(long delayMs) { }

        private cef_client_t* get_default_client(cef_browser_process_handler_t* self)
        {
            CheckSelf(self);

            var m_client = GetDefaultClient();

            return m_client != null ? m_client.ToNative() : null;
        }

        protected virtual CefClient GetDefaultClient() => null;

        private cef_request_context_handler_t* get_default_request_context_handler(cef_browser_process_handler_t* self)
        {
            CheckSelf(self);

            var m_requestContextHandler = GetDefaultRequestContextHandler();
            return m_requestContextHandler != null ? m_requestContextHandler.ToNative() : null;
        }

        protected virtual CefRequestContextHandler GetDefaultRequestContextHandler() => null;
    }
}
