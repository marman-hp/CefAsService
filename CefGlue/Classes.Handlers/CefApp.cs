namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefApp
    {
        private void on_before_command_line_processing(cef_app_t* self, cef_string_t* process_type, cef_command_line_t* command_line)
        {
            CheckSelf(self);

            var processType = cef_string_t.ToString(process_type);

            using (var m_commandLine = CefCommandLine.FromNative(command_line))
            {
                OnBeforeCommandLineProcessing(processType, m_commandLine);
            }
        }

        protected virtual void OnBeforeCommandLineProcessing(string processType, CefCommandLine commandLine)
        {
        }

        private void on_register_custom_schemes(cef_app_t* self, cef_scheme_registrar_t* registrar)
        {
            CheckSelf(self);

            var m_registrar = CefSchemeRegistrar.FromNative(registrar);

            try
            {
                OnRegisterCustomSchemes(m_registrar);
            }
            finally
            {
                m_registrar.ReleaseObject();
            }
        }

        protected virtual void OnRegisterCustomSchemes(CefSchemeRegistrar registrar)
        {
        }

        private cef_resource_bundle_handler_t* get_resource_bundle_handler(cef_app_t* self)
        {
            CheckSelf(self);

            var result = GetResourceBundleHandler();

            return result != null ? result.ToNative() : null;
        }

        protected virtual CefResourceBundleHandler GetResourceBundleHandler()
        {
            return null;
        }

        private cef_browser_process_handler_t* get_browser_process_handler(cef_app_t* self)
        {
            CheckSelf(self);

            var result = GetBrowserProcessHandler();

            return result != null ? result.ToNative() : null;
        }

        protected virtual CefBrowserProcessHandler GetBrowserProcessHandler()
        {
            return null;
        }

        private cef_render_process_handler_t* get_render_process_handler(cef_app_t* self)
        {
            CheckSelf(self);

            var result = GetRenderProcessHandler();

            return result != null ? result.ToNative() : null;
        }

        protected virtual CefRenderProcessHandler GetRenderProcessHandler()
        {
            return null;
        }
    }
}
