using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

namespace Xilium.CefGlue.Headless.Service
{
    internal static class EncoderPluginHost
    {
        private static readonly string[] DefaultPreference = { "x264", "openh264", "vp9", "av1", "x265" };

        private static List<IVideoEncoderPlugin> _plugins;

        internal static IReadOnlyList<IVideoEncoderPlugin> Plugins => _plugins ?? LoadAll();

        internal static string PluginRoot => Path.Combine(AppContext.BaseDirectory, "plugins", "encoders");

        internal static IReadOnlyList<IVideoEncoderPlugin> LoadAll()
        {
            if (_plugins != null)
            {
                return _plugins;
            }

            _plugins = new List<IVideoEncoderPlugin>();
            if (!Directory.Exists(PluginRoot))
            {
                Console.WriteLine($"[Encoders] No plugin folder at {PluginRoot} - only the built-in image formats are available.");
                return _plugins;
            }

            foreach (var folder in Directory.GetDirectories(PluginRoot).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var manifestPath = Path.Combine(folder, "plugin.json");
                    if (!File.Exists(manifestPath))
                    {
                        continue;
                    }

                    using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
                    var assemblyPath = Path.Combine(folder, manifest.RootElement.GetProperty("assembly").GetString());
                    var context = new PluginLoadContext(assemblyPath);
                    var assembly = context.LoadFromAssemblyPath(assemblyPath);

                    foreach (var type in assembly.GetExportedTypes())
                    {
                        if (type.IsClass && !type.IsAbstract && typeof(IVideoEncoderPlugin).IsAssignableFrom(type))
                        {
                            var plugin = (IVideoEncoderPlugin)Activator.CreateInstance(type);
                            if (_plugins.Any(p => string.Equals(p.Id, plugin.Id, StringComparison.OrdinalIgnoreCase)))
                            {
                                Console.WriteLine($"[Encoders] Skipping duplicate encoder id '{plugin.Id}' in {folder}.");
                                continue;
                            }

                            _plugins.Add(plugin);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Encoders] Failed to load plugin from {folder}: {ex.Message}");
                }
            }

            Console.WriteLine($"[Encoders] Loaded: {(_plugins.Count == 0 ? "(none)" : string.Join(", ", _plugins.Select(p => p.Id)))}");
            return _plugins;
        }

        internal static IVideoEncoderPlugin Find(string id)
            => string.IsNullOrEmpty(id) ? null : Plugins.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

        internal static IVideoEncoderPlugin Select(string requestedId)
        {
            var requested = Find(requestedId);
            if (requested != null)
            {
                return requested;
            }

            var fallback = DefaultPreference.Select(Find).FirstOrDefault(p => p != null) ?? Plugins.FirstOrDefault();
            if (!string.IsNullOrEmpty(requestedId))
            {
                Console.WriteLine($"[Encoders] Encoder '{requestedId}' is not installed - using '{fallback?.Id ?? "(none)"}' instead.");
            }

            return fallback;
        }

        internal static string[] DetectAvailable()
            => Plugins.Where(p => p.IsAvailable(out _)).Select(p => p.Id).ToArray();

        private sealed class PluginLoadContext : AssemblyLoadContext
        {
            private readonly AssemblyDependencyResolver _resolver;
            private readonly string _folder;

            public PluginLoadContext(string assemblyPath)
                : base(Path.GetFileNameWithoutExtension(assemblyPath), isCollectible: false)
            {
                _resolver = new AssemblyDependencyResolver(assemblyPath);
                _folder = Path.GetDirectoryName(assemblyPath);
            }

            private static readonly HashSet<string> HostAssemblies = new(
                ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
                    .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                    .Select(Path.GetFileNameWithoutExtension),
                StringComparer.OrdinalIgnoreCase);

            protected override Assembly Load(AssemblyName assemblyName)
            {
                if (HostAssemblies.Contains(assemblyName.Name))
                {
                    return null;
                }

                var path = _resolver.ResolveAssemblyToPath(assemblyName);
                return path != null ? LoadFromAssemblyPath(path) : null;
            }

            protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
            {
                var fileName = unmanagedDllName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? unmanagedDllName : unmanagedDllName + ".dll";
                var local = Path.Combine(_folder, fileName);
                if (File.Exists(local))
                {
                    return LoadUnmanagedDllFromPath(local);
                }

                var resolved = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
                return resolved != null ? LoadUnmanagedDllFromPath(resolved) : IntPtr.Zero;
            }
        }
    }
}
