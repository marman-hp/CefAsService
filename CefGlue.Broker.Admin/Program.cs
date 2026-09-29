using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xilium.CefGlue.Broker.Admin.Components;

namespace Xilium.CefGlue.Broker.Admin
{
    internal static class Program
    {
        private static async Task<int> RunEncoderCommandAsync(string command, string encoderId, string workerExePath)
        {
            if (string.IsNullOrEmpty(workerExePath))
            {
                Console.Error.WriteLine("Worker exe path not set - set CEFGLUE_WORKER_EXE_PATH or save it on the Admin page's Executables tab.");
                return 2;
            }

            var manifest = EncoderManifestStore.Load(workerExePath).FirstOrDefault(m => string.Equals(m.Id, encoderId, StringComparison.OrdinalIgnoreCase));
            if (manifest?.Download == null)
            {
                Console.Error.WriteLine($"No downloadable encoder '{encoderId}' next to {workerExePath}.");
                return 2;
            }

            Console.WriteLine(manifest.Download.Attribution);
            Console.WriteLine($"License: {manifest.Download.LicenseUrl}");
            var (ok, message) = command.Equals("--remove-encoder", StringComparison.OrdinalIgnoreCase)
                ? EncoderDownloader.Remove(manifest)
                : await EncoderDownloader.InstallAsync(manifest);
            (ok ? Console.Out : Console.Error).WriteLine(message);
            return ok ? 0 : 1;
        }

        private static async Task Main(string[] args)
        {
            ConsoleQuickEdit.Disable();

            var autoStartBroker = args.Any(a => string.Equals(a, "--auto-start-broker", StringComparison.OrdinalIgnoreCase));
            var childArgs = args.Where(a => !string.Equals(a, "--auto-start-broker", StringComparison.OrdinalIgnoreCase)).ToArray();

            var adminSettings = new AdminSettingsStore();
            var exePaths = new ExePaths(adminSettings);

            var encoderCommand = Array.FindIndex(args, a => a.Equals("--install-encoder", StringComparison.OrdinalIgnoreCase) || a.Equals("--remove-encoder", StringComparison.OrdinalIgnoreCase));
            if (encoderCommand >= 0)
            {
                Environment.ExitCode = await RunEncoderCommandAsync(args[encoderCommand], encoderCommand + 1 < args.Length ? args[encoderCommand + 1] : null, exePaths.WorkerExePath);
                return;
            }

            var brokerAdminPort = int.TryParse(Environment.GetEnvironmentVariable("CEFGLUE_BROKER_ADMIN_PORT"), out var parsedBrokerAdminPort)
                ? parsedBrokerAdminPort
                : 57401;

            var ownPort = int.TryParse(Environment.GetEnvironmentVariable("CEFGLUE_BROKER_ADMIN_UI_PORT"), out var parsedOwnPort)
                ? parsedOwnPort
                : 57402;

            var workerSource = new RemoteWorkerSource(brokerAdminPort);
            var supervisor = new BrokerSupervisor(exePaths, childArgs, workerSource);

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ContentRootPath = AppContext.BaseDirectory,
            });
            builder.Logging.ClearProviders();

            builder.Services.Configure<Microsoft.Extensions.Hosting.HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(3));
            builder.Services.AddRazorComponents().AddInteractiveServerComponents();
            builder.Services.AddSingleton(supervisor);
            builder.Services.AddSingleton(workerSource);
            builder.Services.AddSingleton(adminSettings);
            builder.Services.AddSingleton(exePaths);
            builder.Services.AddAdminAuth();

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Listen(IPAddress.Loopback, ownPort);
            });

            var app = builder.Build();
            app.UseStaticFiles();
            app.UseAuthentication();
            app.UseRequireLogin();
            app.UseAntiforgery();
            app.MapAdminAuthEndpoints();
            app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

            app.Lifetime.ApplicationStopping.Register(() =>
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[Admin] Shutdown requested - closing in progress...");
                Console.ResetColor();
            });

            Console.WriteLine($"CefGlue.Broker.Admin listening on http://127.0.0.1:{ownPort}/admin (loopback-only)");
            Console.WriteLine($"Managing broker exe '{exePaths.BrokerExePath ?? "(not set - set it on the Admin page)"}', talking to its admin API at http://127.0.0.1:{brokerAdminPort}");

            if (autoStartBroker)
            {
                await supervisor.StartAsync();
            }
            else
            {
                Console.WriteLine("[Admin] --auto-start-broker not given - broker starts stopped; use the Start button on the admin page.");
            }

            await app.RunAsync();
        }
    }
}
