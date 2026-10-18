using System;
using Xilium.CefGlue.Common.Handlers;

namespace Xilium.CefGlue.Headless
{
    public sealed class HeadlessFileDialogHandler : DialogHandler
    {
        public event Action<bool, string[]> FileDialogRequested;

        private CefFileDialogCallback _pendingCallback;

        protected override bool OnFileDialog(CefBrowser browser, CefFileDialogMode mode, string title, string defaultFilePath, string[] acceptFilters, string[] acceptExtensions, string[] acceptDescriptions, CefFileDialogCallback callback)
        {
            try
            {
                if (mode != CefFileDialogMode.Open && mode != CefFileDialogMode.OpenMultiple)
                {
                    return false;
                }

                _pendingCallback?.Cancel();
                _pendingCallback = callback;

                FileDialogRequested?.Invoke(mode == CefFileDialogMode.OpenMultiple, acceptFilters);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HeadlessFileDialogHandler] OnFileDialog failed: {ex}");
                return false;
            }
        }

        public void CompleteFileDialog(string[] filePaths)
        {
            var callback = _pendingCallback;
            _pendingCallback = null;
            callback?.Continue(filePaths ?? Array.Empty<string>());
        }

        public void CancelFileDialog()
        {
            var callback = _pendingCallback;
            _pendingCallback = null;
            callback?.Cancel();
        }
    }
}
