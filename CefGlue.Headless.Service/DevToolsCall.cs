using System;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xilium.CefGlue.Common;

namespace Xilium.CefGlue.Headless.Service
{
    internal static class DevToolsCall
    {
        private static int _nextId = 900_000_000;

        public static async Task<JsonDocument> InvokeAsync(BaseCefBrowser browser, string method, object parameters, TimeSpan timeout)
        {
            var id = Interlocked.Increment(ref _nextId);
            var observer = new ResultObserver(id);
            var registration = browser.AddDevToolsMessageObserver(observer)
                ?? throw new InvalidOperationException("The browser has no host yet.");
            try
            {
                var message = JsonSerializer.Serialize(new { id, method, @params = parameters ?? new { } });
                if (!browser.SendDevToolsMessage(message) && CefRuntime.CurrentlyOn(CefThreadId.UI))
                {
                    throw new InvalidOperationException($"DevTools message {method} was not sent.");
                }

                if (await Task.WhenAny(observer.Result, Task.Delay(timeout)) != observer.Result)
                {
                    throw new TimeoutException($"DevTools {method} did not answer within {timeout.TotalSeconds:0}s.");
                }

                var (success, json) = await observer.Result;
                if (!success)
                {
                    throw new InvalidOperationException($"DevTools {method} failed: {json}");
                }
                return JsonDocument.Parse(json);
            }
            finally
            {
                registration.Dispose();
                GC.KeepAlive(observer);
            }
        }

        private sealed class ResultObserver : CefDevToolsMessageObserver
        {
            private readonly int _id;
            private readonly TaskCompletionSource<(bool Success, string Json)> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public ResultObserver(int id)
            {
                _id = id;
            }

            public Task<(bool Success, string Json)> Result => _result.Task;

            protected override bool OnDevToolsMessage(CefBrowser browser, IntPtr message, int messageSize) => false;

            protected override void OnDevToolsMethodResult(CefBrowser browser, int messageId, bool success, IntPtr result, int resultSize)
            {
                if (messageId == _id)
                {
                    _result.TrySetResult((success, resultSize > 0 ? Marshal.PtrToStringUTF8(result, resultSize) : "{}"));
                }
            }

            protected override void OnDevToolsEvent(CefBrowser browser, string method, IntPtr parameters, int parametersSize)
            {
            }

            protected override void OnDevToolsAgentAttached(CefBrowser browser)
            {
            }

            protected override void OnDevToolsAgentDetached(CefBrowser browser)
            {
            }
        }
    }
}
