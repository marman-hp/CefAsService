using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Xilium.CefGlue.Headless.Server
{
    public enum TicketResult
    {
        Ok,
        Missing,
        Malformed,
        BadSignature,
        Expired,
        WrongScope,
        WrongKey,
        Replayed,
    }

    public static class TicketAuth
    {
        public const string ViewScope = "view";
        public const string InternalScope = "internal";

        public static readonly TimeSpan DefaultLifetime = TimeSpan.FromSeconds(60);

        public const string HeaderName = "X-CefGlue-Ticket";
        public const string WorkerSecretHeaderName = "X-CefGlue-Worker-Secret";

        public static string GenerateSecret() => Base64Url(RandomNumberGenerator.GetBytes(32));

        public static string Mint(string secret, string keyId, string scope, TimeSpan? lifetime = null, DateTimeOffset? now = null)
        {
            var expires = (now ?? DateTimeOffset.UtcNow).Add(lifetime ?? DefaultLifetime).ToUnixTimeSeconds();
            var nonce = Base64Url(RandomNumberGenerator.GetBytes(12));
            var payload = Encoding.UTF8.GetBytes($"{keyId}|{scope}|{expires}|{nonce}");
            return Base64Url(payload) + "." + Base64Url(Sign(secret, payload));
        }

        public static TicketResult Verify(string secret, string ticket, string expectedScope, string expectedKeyId,
            out string nonce, out long expiresUnix, DateTimeOffset? now = null)
        {
            nonce = null;
            expiresUnix = 0;
            if (string.IsNullOrEmpty(ticket))
            {
                return TicketResult.Missing;
            }

            var dot = ticket.IndexOf('.');
            if (dot <= 0 || dot == ticket.Length - 1)
            {
                return TicketResult.Malformed;
            }

            byte[] payload, signature;
            try
            {
                payload = FromBase64Url(ticket[..dot]);
                signature = FromBase64Url(ticket[(dot + 1)..]);
            }
            catch (FormatException)
            {
                return TicketResult.Malformed;
            }

            if (!CryptographicOperations.FixedTimeEquals(signature, Sign(secret, payload)))
            {
                return TicketResult.BadSignature;
            }

            var parts = Encoding.UTF8.GetString(payload).Split('|');
            if (parts.Length != 4 || !long.TryParse(parts[2], out expiresUnix))
            {
                return TicketResult.Malformed;
            }

            if ((now ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() > expiresUnix)
            {
                return TicketResult.Expired;
            }

            if (parts[1] != expectedScope)
            {
                return TicketResult.WrongScope;
            }

            if (!string.IsNullOrEmpty(expectedKeyId) && parts[0] != expectedKeyId)
            {
                return TicketResult.WrongKey;
            }

            nonce = parts[3];
            return TicketResult.Ok;
        }

        public static bool SecretsEqual(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
        }

        private static byte[] Sign(string secret, byte[] payload) => HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), payload);

        private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static byte[] FromBase64Url(string text)
        {
            var padded = text.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
            return Convert.FromBase64String(padded);
        }
    }

    internal sealed class TicketNonceCache
    {
        private readonly Dictionary<string, long> _seen = new();
        private readonly object _lock = new();

        public bool TryConsume(string nonce, long expiresUnix)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            lock (_lock)
            {
                if (_seen.Count > 0)
                {
                    var stale = new List<string>();
                    foreach (var pair in _seen)
                    {
                        if (pair.Value < now)
                        {
                            stale.Add(pair.Key);
                        }
                    }

                    foreach (var key in stale)
                    {
                        _seen.Remove(key);
                    }
                }

                return _seen.TryAdd(nonce, expiresUnix);
            }
        }
    }
}
