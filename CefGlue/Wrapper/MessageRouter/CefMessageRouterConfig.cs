namespace Xilium.CefGlue.Wrapper
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public sealed class CefMessageRouterConfig
    {
        public CefMessageRouterConfig()
        {
            JSQueryFunction = "cefQuery";
            JSCancelFunction = "cefQueryCancel";
            MessageSizeThreshold = CefMessageRouter.ResponseSizeThreshold;
        }

        public string JSQueryFunction { get; set; }

        public string JSCancelFunction { get; set; }

        public int MessageSizeThreshold { get; set; }

        internal bool Validate()
        {
            if (string.IsNullOrEmpty(JSQueryFunction) || string.IsNullOrEmpty(JSCancelFunction))
            {
                return false;
            }

            if (MessageSizeThreshold < 0)
            {
                return false;
            }

            return true;
        }
    }
}
