namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public sealed unsafe partial class CefUrlRequest
    {
        public static CefUrlRequest Create(CefRequest request, CefUrlRequestClient client, CefRequestContext requestContext)
        {
            if (request == null) throw new ArgumentNullException("request");

            var n_request = request.ToNative();
            var n_client = client != null ? client.ToNative() : null;
            var n_requestContext = requestContext != null ? requestContext.ToNative() : null;

            return CefUrlRequest.FromNative(
                cef_urlrequest_t.create(n_request, n_client, n_requestContext)
                );
        }

        public CefRequest GetRequest()
        {
            return CefRequest.FromNative(
                cef_urlrequest_t.get_request(_self)
                );
        }

        public CefUrlRequestClient GetClient()
        {
            return CefUrlRequestClient.FromNative(
                cef_urlrequest_t.get_client(_self)
                );
        }

        public CefUrlRequestStatus RequestStatus
        {
            get { return cef_urlrequest_t.get_request_status(_self); }
        }

        public CefErrorCode RequestError
        {
            get { return cef_urlrequest_t.get_request_error(_self); }
        }

        public CefResponse GetResponse()
        {
            return CefResponse.FromNativeOrNull(
                cef_urlrequest_t.get_response(_self)
                );
        }

        public bool ResponseWasCached
        {
            get
            {
                return cef_urlrequest_t.response_was_cached(_self) != 0;
            }
        }

        public void Cancel()
        {
            cef_urlrequest_t.cancel(_self);
        }
    }
}
