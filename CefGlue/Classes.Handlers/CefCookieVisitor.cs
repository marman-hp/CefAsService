namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xilium.CefGlue.Interop;

    public abstract unsafe partial class CefCookieVisitor
    {
        private int visit(cef_cookie_visitor_t* self, cef_cookie_t* cookie, int count, int total, int* deleteCookie)
        {
            CheckSelf(self);

            var mCookie = CefCookie.FromNative(cookie);
            bool mDelete;

            var result = Visit(mCookie, count, total, out mDelete);

            *deleteCookie = mDelete ? 1 : 0;
            return result ? 1 : 0;
        }

        protected abstract bool Visit(CefCookie cookie, int count, int total, out bool delete);
    }
}
