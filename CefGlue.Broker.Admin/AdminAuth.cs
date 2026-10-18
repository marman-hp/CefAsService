using System;
using System.Security.Cryptography;
using System.Text;

namespace Xilium.CefGlue.Broker.Admin
{
    internal sealed class AdminAuth
    {
        public const int MinPasswordLength = 8;
        private const int Iterations = 210_000;
        private const int SaltBytes = 16;
        private const int HashBytes = 32;
        private const int MaxFailures = 5;
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromSeconds(30);

        private readonly AdminSettingsStore _settings;
        private readonly object _gate = new();
        private int _consecutiveFailures;
        private DateTime _lockedUntilUtc;

        public AdminAuth(AdminSettingsStore settings)
        {
            _settings = settings;
        }

        public bool HasPassword => !string.IsNullOrEmpty(_settings.Snapshot().PasswordHash);

        public string PasswordStamp
        {
            get
            {
                var hash = _settings.Snapshot().PasswordHash;
                return string.IsNullOrEmpty(hash)
                    ? null
                    : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hash)))[..16];
            }
        }

        public TimeSpan? LockoutRemaining
        {
            get
            {
                lock (_gate)
                {
                    var remaining = _lockedUntilUtc - DateTime.UtcNow;
                    return remaining > TimeSpan.Zero ? remaining : null;
                }
            }
        }

        public static string ValidateNewPassword(string password, string confirm)
        {
            if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            {
                return $"Password must be at least {MinPasswordLength} characters.";
            }

            return password == confirm ? null : "The two passwords don't match.";
        }

        public void SetPassword(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltBytes);
            var hash = Derive(password, salt, Iterations);
            _settings.Update(s =>
            {
                s.PasswordHash = Convert.ToBase64String(hash);
                s.PasswordSalt = Convert.ToBase64String(salt);
                s.PasswordIterations = Iterations;
            });
        }

        public bool TryLogin(string password)
        {
            var ok = Verify(password);
            lock (_gate)
            {
                if (ok)
                {
                    _consecutiveFailures = 0;
                }
                else if (++_consecutiveFailures >= MaxFailures)
                {
                    _consecutiveFailures = 0;
                    _lockedUntilUtc = DateTime.UtcNow + LockoutDuration;
                    Console.WriteLine($"[Admin] {MaxFailures} failed login attempts - login locked for {LockoutDuration.TotalSeconds:F0}s.");
                }
            }

            return ok;
        }

        public bool Verify(string password)
        {
            var data = _settings.Snapshot();
            if (string.IsNullOrEmpty(data.PasswordHash) || password == null)
            {
                return false;
            }

            var expected = Convert.FromBase64String(data.PasswordHash);
            var actual = Derive(password, Convert.FromBase64String(data.PasswordSalt), data.PasswordIterations);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }

        private static byte[] Derive(string password, byte[] salt, int iterations) =>
            Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, HashBytes);
    }
}
