using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Xilium.CefGlue.Headless.Service
{
    internal static class RecordingLocks
    {
        private static readonly HashSet<string> TabLocked = new HashSet<string>(StringComparer.Ordinal)
        {
            "resize",
            "mobile",
            "viewmode",
        };

        private static readonly HashSet<string> WorkerLocked = new HashSet<string>(StringComparer.Ordinal)
        {
            "compression",
            "h264quality",
            "overlay",
        };

        public static bool IsLocked(string action, JsonElement root, bool anyTabRecording, bool selectedTabRecording)
        {
            if (action == null || !anyTabRecording)
            {
                return false;
            }

            if (WorkerLocked.Contains(action))
            {
                return true;
            }

            if (!selectedTabRecording)
            {
                return false;
            }

            if (TabLocked.Contains(action))
            {
                return true;
            }

            return action == "screenshot"
                && root.TryGetProperty("fullPage", out var fullPage)
                && fullPage.ValueKind == JsonValueKind.True;
        }
    }
}
