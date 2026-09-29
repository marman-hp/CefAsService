using System;
using System.IO;
using Xilium.CefGlue.Common.Handlers;

namespace Xilium.CefGlue.Headless
{
    public sealed class HeadlessDownloadHandler : DownloadHandler
    {
        public event Action<string, string, string> DownloadCompleted;

        public event Action<string> DownloadFailed;

        private string _pendingPath;
        private string _pendingFileName;
        private bool _pendingReported;

        protected override bool OnBeforeDownload(CefBrowser browser, CefDownloadItem downloadItem, string suggestedName, CefBeforeDownloadCallback callback)
        {
            try
            {
                var safeName = string.IsNullOrWhiteSpace(suggestedName) ? "download" : suggestedName;
                var tempPath = Path.Combine(Path.GetTempPath(), $"cefglue-download-{Guid.NewGuid():N}-{safeName}");

                _pendingPath = tempPath;
                _pendingFileName = safeName;
                _pendingReported = false;

                callback.Continue(tempPath, showDialog: false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HeadlessDownloadHandler] OnBeforeDownload failed: {ex}");
            }

            return true;
        }

        protected override void OnDownloadUpdated(CefBrowser browser, CefDownloadItem downloadItem, CefDownloadItemCallback callback)
        {
            try
            {
                if (_pendingReported || _pendingPath == null || !downloadItem.IsValid)
                {
                    return;
                }

                var isDone = downloadItem.IsComplete;
                var isFailed = downloadItem.IsCanceled || downloadItem.IsInterrupted;

                if (!isDone && !isFailed)
                {
                    return;
                }

                _pendingReported = true;
                var path = _pendingPath;
                var fileName = _pendingFileName;
                _pendingPath = null;
                _pendingFileName = null;

                if (isDone)
                {
                    string mimeType;
                    try
                    {
                        mimeType = downloadItem.MimeType;
                    }
                    catch (Exception)
                    {
                        mimeType = null;
                    }

                    DownloadCompleted?.Invoke(path, fileName, mimeType);
                }
                else
                {
                    DownloadFailed?.Invoke(path);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HeadlessDownloadHandler] OnDownloadUpdated failed: {ex}");
            }
        }
    }
}
