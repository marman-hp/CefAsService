using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

namespace Xilium.CefGlue.Headless.Service
{
    internal static class BrowsingFilterLog
    {
        private static readonly ConcurrentDictionary<string, DateTime> LastLogged = new();
        private static readonly ConcurrentQueue<string> Recent = new();
        private static long _flagged;
        private static long _refused;

        public static long FlaggedCount => Interlocked.Read(ref _flagged);

        public static long RefusedCount => Interlocked.Read(ref _refused);

        public static FilterResult Observe(string url, string kind, string tabId)
        {
            try
            {
                var result = AddressPolicy.Check(url);
                if (!result.Block)
                {
                    return result;
                }

                var enforced = AddressPolicy.IsEnforced(result);
                Interlocked.Increment(ref _flagged);
                if (enforced)
                {
                    Interlocked.Increment(ref _refused);
                }

                var key = $"{result.Reason}|{result.Host}:{result.Port}|{enforced}";
                var now = DateTime.UtcNow;
                if (LastLogged.TryGetValue(key, out var last) && now - last < TimeSpan.FromSeconds(30))
                {
                    return result;
                }

                if (LastLogged.Count > 1000)
                {
                    LastLogged.Clear();
                }

                LastLogged[key] = now;

                var target = result.Port > 0 ? (result.Host.Contains(':') ? $"[{result.Host}]:{result.Port}" : $"{result.Host}:{result.Port}") : result.Host;
                var line = enforced
                    ? $"[Filter] BLOCKED ({result.Reason}): {target} [{kind}, tab {tabId ?? "-"}] - answered 403 Forbidden"
                    : $"[Filter] would block ({result.Reason}): {target} [{kind}, tab {tabId ?? "-"}] - not enforced at level {AddressPolicy.Enforcement}, nothing was refused";
                Console.WriteLine(line);

                Recent.Enqueue($"{now:O} {(enforced ? "blocked" : "flagged")} {result.Reason} {target} {kind}");
                while (Recent.Count > 50 && Recent.TryDequeue(out _))
                {
                }

                return result;
            }
            catch
            {
                return FilterResult.Allowed;
            }
        }
    }

    internal sealed class FilterResourceRequestHandler : CefResourceRequestHandler
    {
        private readonly BrowserSession _session;

        public FilterResourceRequestHandler(BrowserSession session)
        {
            _session = session;
        }

        protected override CefCookieAccessFilter GetCookieAccessFilter(CefBrowser browser, CefFrame frame, CefRequest request) => null;

        protected override CefReturnValue OnBeforeResourceLoad(CefBrowser browser, CefFrame frame, CefRequest request, CefCallback callback)
        {
            BrowsingFilterLog.Observe(request.Url, "resource", _session.Id);
            return CefReturnValue.Continue;
        }

        protected override CefResourceHandler GetResourceHandler(CefBrowser browser, CefFrame frame, CefRequest request)
        {
            try
            {
                if (AddressPolicy.IsEnforced(AddressPolicy.Check(request.Url)))
                {
                    var type = request.ResourceType;
                    return new BlockedResourceHandler(type == CefResourceType.MainFrame || type == CefResourceType.SubFrame);
                }
            }
            catch
            {
            }

            return null;
        }

        protected override void OnResourceRedirect(CefBrowser browser, CefFrame frame, CefRequest request, CefResponse response, ref string newUrl)
        {
            BrowsingFilterLog.Observe(newUrl, "redirect", _session.Id);
        }
    }

    internal sealed class BlockedResourceHandler : CefResourceHandler
    {
        private const string PageHtml =
            "<!doctype html><html><head><meta charset=\"utf-8\"><title>Blocked</title></head>" +
            "<body style=\"margin:0;height:100vh;display:flex;align-items:center;justify-content:center;background:#111827;color:#e5e7eb;font:16px system-ui,sans-serif\">" +
            "<p>Blocked by the server administrator</p></body></html>";

        private static readonly byte[] PageBytes = Encoding.UTF8.GetBytes(PageHtml);

        private readonly byte[] _body;
        private int _position;

        public BlockedResourceHandler(bool isPage)
        {
            _body = isPage ? PageBytes : Array.Empty<byte>();
        }

        protected override bool Open(CefRequest request, out bool handleRequest, CefCallback callback)
        {
            handleRequest = true;
            return true;
        }

        protected override void GetResponseHeaders(CefResponse response, out long responseLength, out string redirectUrl)
        {
            response.Status = 403;
            response.StatusText = "Forbidden";
            response.MimeType = "text/html";
            response.Charset = "utf-8";
            response.SetHeaderByName("Cache-Control", "no-store", true);
            responseLength = _body.Length;
            redirectUrl = null;
        }

        protected override bool Skip(long bytesToSkip, out long bytesSkipped, CefResourceSkipCallback callback)
        {
            bytesSkipped = Math.Min(bytesToSkip, _body.Length - _position);
            _position += (int)bytesSkipped;
            return true;
        }

        protected override bool Read(Stream response, int bytesToRead, out int bytesRead, CefResourceReadCallback callback)
        {
            var count = Math.Min(bytesToRead, _body.Length - _position);
            if (count <= 0)
            {
                bytesRead = 0;
                return false;
            }

            response.Write(_body, _position, count);
            _position += count;
            bytesRead = count;
            return true;
        }

        protected override void Cancel()
        {
        }
    }
}
