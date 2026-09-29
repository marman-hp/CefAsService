using CefGlue;

namespace Xilium.CefGlue
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Globalization;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public static unsafe class CefRuntime
    {
        private static readonly CefRuntimePlatform _platform;

        private static bool _loaded;
        private static bool _initialized;

        static CefRuntime()
        {
            _platform = DetectPlatform();
            NativeLibsLoader.Install();
        }

        #region Platform Detection
        private static CefRuntimePlatform DetectPlatform()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return CefRuntimePlatform.Windows;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return CefRuntimePlatform.MacOS;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return CefRuntimePlatform.Linux;
            }

            throw new PlatformNotSupportedException();
        }

        public static CefRuntimePlatform Platform
        {
            get { return _platform; }
        }
        #endregion

        public static bool IsInitialized => _initialized;

        public static void Load()
        {
            if (Platform != CefRuntimePlatform.Windows)
            {
                Load(string.Empty);
                return;
            }

            var libcefDir = AppDomain.CurrentDomain.BaseDirectory;
            Load(System.IO.Path.Combine(libcefDir, @"runtimes\win-x64\native"));
        }

        public static void Load(string path)
        {
            if (_loaded) return;

            if (!string.IsNullOrEmpty(path))
            {
                if (Platform == CefRuntimePlatform.Windows)
                    LoadLibraryWindows(path);
                else
                    throw new PlatformNotSupportedException("CEF Runtime can't be initialized on altered path on this platform. Use CefRuntime.Load() instead.");
            }

            CheckVersion();

            _loaded = true;
        }

        private static void LoadLibraryWindows(string path)
        {
            Xilium.CefGlue.Platform.Windows.NativeMethods.LoadLibraryEx(
                System.IO.Path.Combine(path, "libcef.dll"),
                IntPtr.Zero,
                Xilium.CefGlue.Platform.Windows.LoadLibraryFlags.LOAD_WITH_ALTERED_SEARCH_PATH
                );
        }

        #region cef_version

        public static string ChromeVersion
        {
            get
            {
                return string.Format("{0}.{1}.{2}.{3}", libcef.CHROME_VERSION_MAJOR, libcef.CHROME_VERSION_MINOR, libcef.CHROME_VERSION_BUILD, libcef.CHROME_VERSION_PATCH);
            }
        }

        private static void CheckVersion()
        {
            CheckVersionByApiHash();
        }

        private static void CheckVersionByApiHash()
        {
            var libCefFile = CefRuntimeLocator.FindLibrary();
            if (libCefFile != null)
            {
                NativeLibrary.TryLoad(libCefFile, out _);
            }

            string actual;
            try
            {
                var n_actual = libcef.api_hash(libcef.CEF_API_VERSION, 0);
                actual = n_actual != null ? new string(n_actual) : null;
            }
            catch (EntryPointNotFoundException ex)
            {
                throw new NotSupportedException("cef_api_hash call is not supported.", ex);
            }
            catch (DllNotFoundException dllEx)
            {
                throw new NotSupportedException($"Can't find CEF in \"{AppDomain.CurrentDomain.BaseDirectory}\"", dllEx);
            }
            if (string.IsNullOrEmpty(actual)) throw new NotSupportedException();

            string expected;
            switch (Platform)
            {
                case CefRuntimePlatform.Windows: expected = libcef.CEF_API_HASH_PLATFORM_WIN; break;
                case CefRuntimePlatform.MacOS: expected = libcef.CEF_API_HASH_PLATFORM_MACOS; break;
                case CefRuntimePlatform.Linux: expected = libcef.CEF_API_HASH_PLATFORM_LINUX; break;
                default: throw new PlatformNotSupportedException();
            }

            if (string.Compare(actual, expected, StringComparison.OrdinalIgnoreCase) != 0)
            {
                var expectedVersion = libcef.CEF_VERSION;
                throw ExceptionBuilder.RuntimeVersionApiHashMismatch(actual, expected, expectedVersion);
            }
        }

        #endregion

        #region cef_app

        public static int ExecuteProcess(CefMainArgs args, CefApp application, IntPtr windowsSandboxInfo)
        {
            LoadIfNeed();

            var n_args = args.ToNative();
            var n_app = application != null ? application.ToNative() : null;

            try
            {
                return libcef.execute_process(n_args, n_app, (void*)windowsSandboxInfo);
            }
            finally
            {
                CefMainArgs.Free(n_args);
            }
        }

        [Obsolete]
        public static int ExecuteProcess(CefMainArgs args, CefApp application)
        {
            return ExecuteProcess(args, application, IntPtr.Zero);
        }

        public static void Initialize(CefMainArgs args, CefSettings settings, CefApp application, IntPtr windowsSandboxInfo)
        {
            LoadIfNeed();

            if (args == null) throw new ArgumentNullException("args");
            if (settings == null) throw new ArgumentNullException("settings");

            if (_initialized) throw ExceptionBuilder.CefRuntimeAlreadyInitialized();

            var n_main_args = args.ToNative();
            var n_settings = settings.ToNative();
            var n_app = application != null ? application.ToNative() : null;

            try
            {
                if (libcef.initialize(n_main_args, n_settings, n_app, (void*)windowsSandboxInfo) != 0)
                {
                    _initialized = true;
                }
                else
                {
                    throw ExceptionBuilder.CefRuntimeFailedToInitialize();
                }
            }
            finally
            {
                CefMainArgs.Free(n_main_args);
                CefSettings.Free(n_settings);
            }
        }

        [Obsolete("Use Initialize(CefMainArgs,CefSettings,CefApp,IntPtr) overload instead.")]
        public static void Initialize(CefMainArgs args, CefSettings settings, CefApp application)
        {
            Initialize(args, settings, application, IntPtr.Zero);
        }

        public static void Shutdown(bool skipGC = false)
        {
            if (!_initialized) return;

            if (!skipGC)
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                GC.WaitForPendingFinalizers();
            }

            libcef.shutdown();
        }

        public static void DoMessageLoopWork()
        {
            libcef.do_message_loop_work();
        }

        public static void RunMessageLoop()
        {
            libcef.run_message_loop();
        }

        public static void QuitMessageLoop()
        {
            libcef.quit_message_loop();
        }

        public static void SetOSModalLoop(bool osModalLoop)
        {
            libcef.set_osmodal_loop(osModalLoop ? 1 : 0);
        }

        #endregion

        #region cef_task

        public static bool CurrentlyOn(CefThreadId threadId)
        {
            return libcef.currently_on(threadId) != 0;
        }

        public static bool PostTask(CefThreadId threadId, CefTask task)
        {
            if (task == null) throw new ArgumentNullException("task");

            return libcef.post_task(threadId, task.ToNative()) != 0;
        }

        public static bool PostTask(CefThreadId threadId, CefTask task, long delay)
        {
            if (task == null) throw new ArgumentNullException("task");

            return libcef.post_delayed_task(threadId, task.ToNative(), delay) != 0;
        }
        #endregion

        #region cef_origin_whitelist

        public static bool AddCrossOriginWhitelistEntry(string sourceOrigin, string targetProtocol, string targetDomain, bool allowTargetSubdomains)
        {
            if (string.IsNullOrEmpty("sourceOrigin")) throw new ArgumentNullException("sourceOrigin");
            if (string.IsNullOrEmpty("targetProtocol")) throw new ArgumentNullException("targetProtocol");

            fixed (char* sourceOrigin_ptr = sourceOrigin)
            fixed (char* targetProtocol_ptr = targetProtocol)
            fixed (char* targetDomain_ptr = targetDomain)
            {
                var n_sourceOrigin = new cef_string_t(sourceOrigin_ptr, sourceOrigin.Length);
                var n_targetProtocol = new cef_string_t(targetProtocol_ptr, targetProtocol.Length);
                var n_targetDomain = new cef_string_t(targetDomain_ptr, targetDomain != null ? targetDomain.Length : 0);

                return libcef.add_cross_origin_whitelist_entry(
                    &n_sourceOrigin,
                    &n_targetProtocol,
                    &n_targetDomain,
                    allowTargetSubdomains ? 1 : 0
                    ) != 0;
            }
        }

        public static bool RemoveCrossOriginWhitelistEntry(string sourceOrigin, string targetProtocol, string targetDomain, bool allowTargetSubdomains)
        {
            if (string.IsNullOrEmpty("sourceOrigin")) throw new ArgumentNullException("sourceOrigin");
            if (string.IsNullOrEmpty("targetProtocol")) throw new ArgumentNullException("targetProtocol");

            fixed (char* sourceOrigin_ptr = sourceOrigin)
            fixed (char* targetProtocol_ptr = targetProtocol)
            fixed (char* targetDomain_ptr = targetDomain)
            {
                var n_sourceOrigin = new cef_string_t(sourceOrigin_ptr, sourceOrigin.Length);
                var n_targetProtocol = new cef_string_t(targetProtocol_ptr, targetProtocol.Length);
                var n_targetDomain = new cef_string_t(targetDomain_ptr, targetDomain != null ? targetDomain.Length : 0);

                return libcef.remove_cross_origin_whitelist_entry(
                    &n_sourceOrigin,
                    &n_targetProtocol,
                    &n_targetDomain,
                    allowTargetSubdomains ? 1 : 0
                    ) != 0;
            }
        }

        public static bool ClearCrossOriginWhitelist()
        {
            return libcef.clear_cross_origin_whitelist() != 0;
        }

        #endregion

        #region cef_scheme

        public static bool RegisterSchemeHandlerFactory(string schemeName, string domainName, CefSchemeHandlerFactory factory)
        {
            if (string.IsNullOrEmpty(schemeName)) throw new ArgumentNullException("schemeName");
            if (factory == null) throw new ArgumentNullException("factory");

            fixed (char* schemeName_str = schemeName)
            fixed (char* domainName_str = domainName)
            {
                var n_schemeName = new cef_string_t(schemeName_str, schemeName.Length);
                var n_domainName = new cef_string_t(domainName_str, domainName != null ? domainName.Length : 0);

                return libcef.register_scheme_handler_factory(&n_schemeName, &n_domainName, factory.ToNative()) != 0;
            }
        }

        public static bool ClearSchemeHandlerFactories()
        {
            return libcef.clear_scheme_handler_factories() != 0;
        }

        #endregion

        #region cef_trace

        public static bool BeginTracing(string categories = null, CefCompletionCallback callback = null)
        {
            fixed (char* categories_str = categories)
            {
                var n_categories = new cef_string_t(categories_str, categories != null ? categories.Length : 0);
                var n_callback = callback != null ? callback.ToNative() : null;
                return libcef.begin_tracing(&n_categories, n_callback) != 0;
            }
        }

        public static bool EndTracing(string tracingFile = null, CefEndTracingCallback callback = null)
        {
            fixed (char* tracingFile_str = tracingFile)
            {
                var n_tracingFile = new cef_string_t(tracingFile_str, tracingFile != null ? tracingFile.Length : 0);
                var n_callback = callback != null ? callback.ToNative() : null;
                return libcef.end_tracing(&n_tracingFile, n_callback) != 0;
            }
        }

        public static long NowFromSystemTraceTime()
        {
            return libcef.now_from_system_trace_time();
        }

        #endregion

        #region cef_parser

        public static bool ParseUrl(string url, out CefUrlParts parts)
        {
            fixed (char* url_str = url)
            {
                var n_url = new cef_string_t(url_str, url != null ? url.Length : 0);
                var n_parts = cef_urlparts_t.Alloc();

                var result = libcef.parse_url(&n_url, n_parts) != 0;

                parts = result ? CefUrlParts.FromNative(n_parts) : null;
                cef_urlparts_t.Free(n_parts);
                return result;
            }
        }

        public static bool CreateUrl(CefUrlParts parts, out string url)
        {
            if (parts == null) throw new ArgumentNullException("parts");

            var n_parts = parts.ToNative();
            var n_url = new cef_string_t();

            var result = libcef.create_url(n_parts, &n_url) != 0;

            url = result ? cef_string_t.ToString(&n_url) : null;

            cef_urlparts_t.Free(n_parts);
            libcef.string_clear(&n_url);

            return result;
        }

        public static string GetMimeType(string extension)
        {
            fixed (char* extension_str = extension)
            {
                var n_extension = new cef_string_t(extension_str, extension != null ? extension.Length : 0);

                var n_result = libcef.get_mime_type(&n_extension);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public static string[] GetExtensionsForMimeType(string mimeType)
        {
            fixed (char* mimeType_str = mimeType)
            {
                var n_mimeType = new cef_string_t(mimeType_str, mimeType != null ? mimeType.Length : 0);

                var n_list = libcef.string_list_alloc();
                libcef.get_extensions_for_mime_type(&n_mimeType, n_list);
                var result = cef_string_list.ToArray(n_list);
                libcef.string_list_free(n_list);
                return result;
            }
        }

        public static unsafe string Base64Encode(void* data, int size)
        {
            var n_result = libcef.base64_encode(data, (UIntPtr)size);
            return cef_string_userfree.ToString(n_result);
        }

        public static string Base64Encode(byte[] bytes, int offset, int length)
        {

            fixed (byte* bytes_ptr = &bytes[offset])
            {
                return Base64Encode(bytes_ptr, length);
            }
        }

        public static string Base64Encode(byte[] bytes)
        {
            return Base64Encode(bytes, 0, bytes.Length);
        }

        public static CefBinaryValue Base64Decode(string data)
        {
            fixed (char* data_str = data)
            {
                var n_data = new cef_string_t(data_str, data != null ? data.Length : 0);
                return CefBinaryValue.FromNative(libcef.base64_decode(&n_data));
            }
        }

        public static string UriEncode(string text, bool usePlus)
        {
            fixed (char* text_str = text)
            {
                var n_text = new cef_string_t(text_str, text != null ? text.Length : 0);

                var n_result = libcef.uriencode(&n_text, usePlus ? 1 : 0);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public static string UriDecode(string text, bool convertToUtf8, CefUriUnescapeRules unescapeRule)
        {
            fixed (char* text_str = text)
            {
                var n_text = new cef_string_t(text_str, text != null ? text.Length : 0);

                var n_result = libcef.uridecode(&n_text, convertToUtf8 ? 1 : 0, unescapeRule);
                return cef_string_userfree.ToString(n_result);
            }
        }

        public static CefValue ParseJson(string value, CefJsonParserOptions options)
        {
            fixed (char* value_str = value)
            {
                var n_value = new cef_string_t(value_str, value != null ? value.Length : 0);
                var n_result = libcef.parse_json(&n_value, options);
                return CefValue.FromNativeOrNull(n_result);
            }
        }

        public static CefValue ParseJson(IntPtr json, int jsonSize, CefJsonParserOptions options)
        {
            var n_result = libcef.parse_json_buffer((void*)json, checked((UIntPtr)jsonSize), options);
            return CefValue.FromNativeOrNull(n_result);
        }

        public static CefValue ParseJsonAndReturnError(string value, CefJsonParserOptions options, out string errorMessage)
        {
            fixed (char* value_str = value)
            {
                var n_value = new cef_string_t(value_str, value != null ? value.Length : 0);

                cef_string_t n_error_msg;
                var n_result = libcef.parse_jsonand_return_error(&n_value, options, &n_error_msg);

                var result = CefValue.FromNativeOrNull(n_result);
                errorMessage = cef_string_userfree.ToString((cef_string_userfree*)&n_error_msg);
                return result;
            }
        }

        public static string WriteJson(CefValue value, CefJsonWriterOptions options)
        {
            if (value == null) throw new ArgumentNullException("value");
            var n_result = libcef.write_json(value.ToNative(), options);
            return cef_string_userfree.ToString(n_result);
        }

        public static bool ResolveUrl(string baseUrl,
            string relativeUrl,
            out string? resolvedUrl)
        {
            fixed (char* baseUrl_str = baseUrl)
            fixed (char* relativeUrl_str = relativeUrl)
            {
                var n_baseUrl = new cef_string_t(baseUrl_str, baseUrl != null ? baseUrl.Length : 0);
                var n_relativeUrl = new cef_string_t(relativeUrl_str, relativeUrl != null ? relativeUrl.Length : 0);

                cef_string_t n_resolvedUrl = default;
                var result = libcef.resolve_url(&n_baseUrl, &n_relativeUrl, &n_resolvedUrl) != 0;

                if (result)
                {
                    resolvedUrl = cef_string_t.ToString(&n_resolvedUrl);
                }
                else
                {
                    resolvedUrl = null;
                }

                libcef.string_clear(&n_resolvedUrl);
                return result;
            }
        }

        #endregion

        #region cef_v8

        public static bool RegisterExtension(string extensionName, string javascriptCode, CefV8Handler handler)
        {
            if (string.IsNullOrEmpty(extensionName)) throw new ArgumentNullException("extensionName");
            if (string.IsNullOrEmpty(javascriptCode)) throw new ArgumentNullException("javascriptCode");

            fixed (char* extensionName_str = extensionName)
            fixed (char* javascriptCode_str = javascriptCode)
            {
                var n_extensionName = new cef_string_t(extensionName_str, extensionName.Length);
                var n_javascriptCode = new cef_string_t(javascriptCode_str, javascriptCode.Length);

                return libcef.register_extension(&n_extensionName, &n_javascriptCode, handler != null ? handler.ToNative() : null) != 0;
            }
        }

        #endregion

        #region cef_path_util

        public static string GetPath(CefPathKey pathKey)
        {
            var n_value = new cef_string_t();
            var success = libcef.get_path(pathKey, &n_value) != 0;
            var value = cef_string_t.ToString(&n_value);
            libcef.string_clear(&n_value);
            if (!success)
            {
                throw new InvalidOperationException(
                    string.Format(CultureInfo.InvariantCulture, "Failed to get path for key {0}.", pathKey)
                    );
            }
            return value;
        }

        #endregion

        #region cef_process_util

        public static bool LaunchProcess(CefCommandLine commandLine)
        {
            if (commandLine == null) throw new ArgumentNullException("commandLine");

            return libcef.launch_process(commandLine.ToNative()) != 0;
        }

        #endregion

        #region cef_sandbox_win
        #endregion

        #region cef_ssl_info

        public static bool IsCertStatusError(CefCertStatus status)
        {
            return libcef.is_cert_status_error(status) != 0;
        }

        #endregion

        #region cef_crash_util

        public static bool CrashReportingEnabled()
        {
            return libcef.crash_reporting_enabled() != 0;
        }

        public static void SetCrashKeyValue(string key, string value)
        {
            fixed (char* key_ptr = key)
            fixed (char* value_ptr = value)
            {
                var n_key = new cef_string_t(key_ptr, key.Length);
                var n_value = new cef_string_t(value_ptr, value != null ? value.Length : 0);
                libcef.set_crash_key_value(&n_key, &n_value);
            }
        }

        #endregion

        #region file_util

        public static void LoadCrlSetsFile(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            fixed (char* path_ptr = path)
            {
                var n_path = new cef_string_t(path_ptr, path.Length);
                libcef.load_crlsets_file(&n_path);
            }
        }

        #endregion

        #region i18n

        public static bool IsRtl() => libcef.is_rtl() != 0;

        #endregion

        private static void LoadIfNeed()
        {
            if (!_loaded) Load();
        }
    }
}
