using System;
using System.Threading.Tasks;

namespace Xilium.CefGlue.Headless.Service
{
    internal static class FaviconFetcher
    {
        private const uint MaxImageSize = 32;

        public static async Task<string> TryFetchAsDataUriAsync(CefBrowserHost host, string[] iconUrls)
        {
            if (host == null || iconUrls == null)
            {
                return null;
            }

            foreach (var url in iconUrls)
            {
                if (string.IsNullOrEmpty(url))
                {
                    continue;
                }

                var scheme = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Scheme : null;
                if (scheme != Uri.UriSchemeHttp && scheme != Uri.UriSchemeHttps && scheme != "data")
                {
                    continue;
                }

                if (scheme != "data" && AddressPolicy.Check(url).Block)
                {
                    Console.WriteLine($"[Filter] favicon not fetched (blocked address): {uri.Host}");
                    continue;
                }

                var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                try
                {
                    host.DownloadImage(url, true, MaxImageSize, false, new Callback(completion));
                }
                catch
                {
                    continue;
                }

                var result = await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(8))) == completion.Task ? completion.Task.Result : null;
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private sealed class Callback : CefDownloadImageCallback
        {
            private readonly TaskCompletionSource<string> _done;

            public Callback(TaskCompletionSource<string> done) => _done = done;

            protected override void OnDownloadImageFinished(string imageUrl, int httpStatusCode, CefImage image)
            {
                try
                {
                    byte[] best = null;
                    var bestPixels = 0;
                    foreach (var scale in new[] { 1.0f, 1.25f, 1.5f, 2.0f, 3.0f, 4.0f })
                    {
                        int width = 0, height = 0;
                        using var png = image?.GetAsPng(scale, true, out width, out height);
                        if (png != null && width * height > bestPixels)
                        {
                            bestPixels = width * height;
                            best = png.ToArray();
                        }
                    }

                    _done.TrySetResult(best is { Length: > 0 } ? "data:image/png;base64," + Convert.ToBase64String(best) : null);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Favicon] PNG conversion failed: {ex.Message}");
                    _done.TrySetResult(null);
                }
            }
        }
    }
}
