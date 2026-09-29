namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Xilium.CefGlue.Interop;

    public sealed unsafe class CefSettings
    {
        public CefSettings()
        {
            BackgroundColor = new CefColor(255, 255, 255, 255);
        }

        public bool NoSandbox { get; set; }

        public string BrowserSubprocessPath { get; set; }

        public string FrameworkDirPath { get; set; }

        public string MainBundlePath { get; set; }

        public bool MultiThreadedMessageLoop { get; set; }

        public bool ExternalMessagePump { get; set; }

        public bool WindowlessRenderingEnabled { get; set; }

        public bool CommandLineArgsDisabled { get; set; }

        public string CachePath { get; set; }

        public string RootCachePath { get; set; }

        public bool PersistSessionCookies { get; set; }

        public string UserAgent { get; set; }

        public string UserAgentProduct { get; set; }

        public string Locale { get; set; }

        public string LogFile { get; set; }

        public CefLogSeverity LogSeverity { get; set; }

        public string JavaScriptFlags { get; set; }

        public string ResourcesDirPath { get; set; }

        public string LocalesDirPath { get; set; }

        public int RemoteDebuggingPort { get; set; }

        public int UncaughtExceptionStackSize { get; set; }

        public CefColor BackgroundColor { get; set; }

        public string AcceptLanguageList { get; set; }

        public string CookieableSchemesList { get; set; }

        public bool CookieableSchemesExcludeDefaults { get; set; }

        public string ChromePolicyId { get; set; }

        public int ChromeAppIconId { get; set; }

        public bool DisableSignalHandlers { get; set; }

        public CefLogItems LogItems { get; set; }

        public bool UseViewsDefaultPopup { get; set; }

        internal cef_settings_t* ToNative()
        {
            var ptr = cef_settings_t.Alloc();
            ptr->no_sandbox = NoSandbox ? 1 : 0;
            cef_string_t.Copy(BrowserSubprocessPath, &ptr->browser_subprocess_path);
            cef_string_t.Copy(FrameworkDirPath, &ptr->framework_dir_path);
            cef_string_t.Copy(MainBundlePath, &ptr->main_bundle_path);
            ptr->multi_threaded_message_loop = MultiThreadedMessageLoop ? 1 : 0;
            ptr->windowless_rendering_enabled = WindowlessRenderingEnabled ? 1 : 0;
            ptr->external_message_pump = ExternalMessagePump ? 1 : 0;
            ptr->command_line_args_disabled = CommandLineArgsDisabled ? 1 : 0;
            cef_string_t.Copy(CachePath, &ptr->cache_path);
            cef_string_t.Copy(RootCachePath, &ptr->root_cache_path);
            ptr->persist_session_cookies = PersistSessionCookies ? 1 : 0;
            cef_string_t.Copy(UserAgent, &ptr->user_agent);
            cef_string_t.Copy(UserAgentProduct, &ptr->user_agent_product);
            cef_string_t.Copy(Locale, &ptr->locale);
            cef_string_t.Copy(LogFile, &ptr->log_file);
            ptr->log_severity = LogSeverity;
            ptr->log_items = LogItems;
            cef_string_t.Copy(JavaScriptFlags, &ptr->javascript_flags);
            cef_string_t.Copy(ResourcesDirPath, &ptr->resources_dir_path);
            cef_string_t.Copy(LocalesDirPath, &ptr->locales_dir_path);
            ptr->remote_debugging_port = RemoteDebuggingPort;
            ptr->uncaught_exception_stack_size = UncaughtExceptionStackSize;
            ptr->background_color = BackgroundColor.ToArgb();
            cef_string_t.Copy(AcceptLanguageList, &ptr->accept_language_list);
            cef_string_t.Copy(CookieableSchemesList, &ptr->cookieable_schemes_list);
            ptr->cookieable_schemes_exclude_defaults = CookieableSchemesExcludeDefaults ? 1 : 0;
            cef_string_t.Copy(ChromePolicyId, &ptr->chrome_policy_id);
            ptr->chrome_app_icon_id = ChromeAppIconId;
            ptr->disable_signal_handlers = DisableSignalHandlers ? 1 : 0;
            ptr->use_views_default_popup = UseViewsDefaultPopup ? 1 : 0;
            return ptr;
        }

        private static void Clear(cef_settings_t* ptr)
        {
            libcef.string_clear(&ptr->browser_subprocess_path);
            libcef.string_clear(&ptr->framework_dir_path);
            libcef.string_clear(&ptr->main_bundle_path);
            libcef.string_clear(&ptr->cache_path);
            libcef.string_clear(&ptr->root_cache_path);
            libcef.string_clear(&ptr->user_agent);
            libcef.string_clear(&ptr->user_agent_product);
            libcef.string_clear(&ptr->locale);
            libcef.string_clear(&ptr->log_file);
            libcef.string_clear(&ptr->javascript_flags);
            libcef.string_clear(&ptr->resources_dir_path);
            libcef.string_clear(&ptr->locales_dir_path);
            libcef.string_clear(&ptr->accept_language_list);
            libcef.string_clear(&ptr->cookieable_schemes_list);
        }

        internal static void Free(cef_settings_t* ptr)
        {
            Clear(ptr);
            cef_settings_t.Free(ptr);
        }
    }
}
