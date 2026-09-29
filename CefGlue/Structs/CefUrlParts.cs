using System.Runtime.InteropServices;

namespace Xilium.CefGlue
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Xilium.CefGlue.Interop;

    public sealed class CefUrlParts
    {
        public string Spec { get; set; }

        public string Scheme { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }

        public string Host { get; set; }

        public string Port { get; set; }

        public string Origin { get; set; }

        public string Path { get; set; }

        public string Query { get; set; }

        public string Fragment { get; set; }

        internal static unsafe CefUrlParts FromNative(cef_urlparts_t* n_parts)
        {
            return new CefUrlParts
            {
                Spec = cef_string_t.ToString(&n_parts->spec),
                Scheme = cef_string_t.ToString(&n_parts->scheme),
                UserName = cef_string_t.ToString(&n_parts->username),
                Password = cef_string_t.ToString(&n_parts->password),
                Host = cef_string_t.ToString(&n_parts->host),
                Port = cef_string_t.ToString(&n_parts->port),
                Origin = cef_string_t.ToString(&n_parts->origin),
                Path = cef_string_t.ToString(&n_parts->path),
                Query = cef_string_t.ToString(&n_parts->query),
                Fragment = cef_string_t.ToString(&n_parts->fragment)
            };
        }

        internal unsafe cef_urlparts_t* ToNative()
        {
            var result = cef_urlparts_t.Alloc();
            cef_string_t.Copy(Spec, &result->spec);
            cef_string_t.Copy(Scheme, &result->scheme);
            cef_string_t.Copy(UserName, &result->username);
            cef_string_t.Copy(Password, &result->password);
            cef_string_t.Copy(Host, &result->host);
            cef_string_t.Copy(Port, &result->port);
            cef_string_t.Copy(Origin, &result->origin);
            cef_string_t.Copy(Path, &result->path);
            cef_string_t.Copy(Query, &result->query);
            cef_string_t.Copy(Fragment, &result->fragment);
            return result;
        }
    }
}
