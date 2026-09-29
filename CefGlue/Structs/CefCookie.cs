namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Xilium.CefGlue.Interop;

    public sealed unsafe class CefCookie
    {
        public CefCookie()
        { }

        public string Name { get; set; }

        public string Value { get; set; }

        public string Domain { get; set; }

        public string Path { get; set; }

        public bool Secure { get; set; }

        public bool HttpOnly { get; set; }

        public CefBaseTime Creation { get; set; }

        public CefBaseTime LastAccess { get; set; }

        public CefBaseTime? Expires { get; set; }

        public CefCookieSameSite SameSite { get; set; }

        public CefCookiePriority Priority { get; set; }

        internal static CefCookie FromNative(cef_cookie_t* ptr)
        {
            return new CefCookie
                {
                    Name = cef_string_t.ToString(&ptr->name),
                    Value = cef_string_t.ToString(&ptr->value),
                    Domain = cef_string_t.ToString(&ptr->domain),
                    Path = cef_string_t.ToString(&ptr->path),
                    Secure = ptr->secure != 0,
                    HttpOnly = ptr->httponly != 0,
                    Creation = ptr->creation,
                    LastAccess = ptr->last_access,
                    Expires = ptr->has_expires != 0 ? ptr->expires : null,
                    SameSite = ptr->same_site,
                    Priority = ptr->priority,
                };
        }

        internal cef_cookie_t* ToNative()
        {
            var ptr = cef_cookie_t.Alloc();

            cef_string_t.Copy(Name, &ptr->name);
            cef_string_t.Copy(Value, &ptr->value);
            cef_string_t.Copy(Domain, &ptr->domain);
            cef_string_t.Copy(Path, &ptr->path);
            ptr->secure = Secure ? 1 : 0;
            ptr->httponly = HttpOnly ? 1 : 0;
            ptr->creation = Creation;
            ptr->last_access = LastAccess;
            ptr->has_expires = Expires != null ? 1 : 0;
            ptr->expires = Expires != null ? Expires.Value : default(CefBaseTime);
            ptr->same_site = SameSite;
            ptr->priority = Priority;

            return ptr;
        }

        internal static void Free(cef_cookie_t* ptr)
        {
            cef_cookie_t.Clear((cef_cookie_t*)ptr);
            cef_cookie_t.Free((cef_cookie_t*)ptr);
        }
    }
}
