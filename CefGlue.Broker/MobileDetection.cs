using System;
using Microsoft.AspNetCore.Http;

namespace Xilium.CefGlue.Broker
{
    internal static class MobileDetection
    {
        private static readonly string[] MobileTokens =
        {
            "Android", "iPhone", "iPad", "iPod", "Windows Phone", "Mobile",
        };

        public static bool ShouldServeMobile(HttpRequest request)
        {
            var uiOverride = request.Query["ui"].ToString();

            if (string.Equals(uiOverride, "mobile", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(uiOverride, "desktop", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var userAgent = request.Headers["User-Agent"].ToString();

            if (string.IsNullOrEmpty(userAgent))
            {
                return false;
            }

            foreach (var token in MobileTokens)
            {
                if (userAgent.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
