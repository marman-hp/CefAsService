using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Xilium.CefGlue.Broker
{
    internal static class SelfSignedCertificate
    {
        internal static readonly string CertPath = Path.Combine(AppContext.BaseDirectory, "broker-dev-cert.pfx");

        internal const string CertPassword = "cefglue-dev-only";

        internal static X509Certificate2 GetOrCreate()
        {
            X509Certificate2 cert;

            if (File.Exists(CertPath))
            {
                try
                {
                    var existing = X509CertificateLoader.LoadPkcs12FromFile(CertPath, CertPassword, X509KeyStorageFlags.Exportable);
                    cert = existing.NotAfter > DateTime.UtcNow ? existing : CreateAndCache();
                }
                catch
                {
                    cert = CreateAndCache();
                }
            }
            else
            {
                cert = CreateAndCache();
            }

            EnsureTrustedOnThisMachine(cert);
            return cert;
        }

        private static void EnsureTrustedOnThisMachine(X509Certificate2 cert)
        {
            try
            {
                using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadWrite);

                if (store.Certificates.Any(c => c.Thumbprint == cert.Thumbprint))
                {
                    return;
                }

                store.Add(cert);
                Console.WriteLine($"[Broker] Installed this machine's own self-signed certificate into the Trusted Root store (thumbprint: {cert.Thumbprint}) - https://localhost:{HttpsPortForLog} and this machine's own LAN IPs on that port are now warning-free FROM THIS MACHINE ONLY.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Broker] Could not install the self-signed certificate as trusted on this machine ({ex.Message}) - falling back to the usual browser warning, nothing else affected.");
            }
        }

        private const string HttpsPortForLog = "57443";

        private static X509Certificate2 CreateAndCache()
        {
            using var rsa = RSA.Create(2048);

            var request = new CertificateRequest(
                "CN=CefAsService Broker (local dev only)",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(certificateAuthority: false, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, critical: true));
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") },
                    critical: false));

            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName("localhost");
            sanBuilder.AddIpAddress(IPAddress.Loopback);
            sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);

            foreach (var ip in GetLocalIPv4Addresses())
            {
                sanBuilder.AddIpAddress(ip);
            }

            request.CertificateExtensions.Add(sanBuilder.Build());

            var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
            var notAfter = DateTimeOffset.UtcNow.AddYears(2);
            using var cert = request.CreateSelfSigned(notBefore, notAfter);

            var pfxBytes = cert.Export(X509ContentType.Pfx, CertPassword);
            File.WriteAllBytes(CertPath, pfxBytes);

            Console.WriteLine($"[Broker] Generated a new self-signed HTTPS certificate covering: localhost, 127.0.0.1" +
                string.Join("", Array.ConvertAll(GetLocalIPv4Addresses(), ip => $", {ip}")));

            return X509CertificateLoader.LoadPkcs12(pfxBytes, CertPassword, X509KeyStorageFlags.Exportable);
        }

        internal static X509Certificate2 TryLoadTrustedLocalhostCert()
        {
            try
            {
                using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadOnly);

                var now = DateTime.Now;
                var cert = store.Certificates
                    .Where(c => c.Subject == "CN=localhost" && c.HasPrivateKey && now >= c.NotBefore && now <= c.NotAfter)
                    .OrderByDescending(c => c.NotBefore)
                    .FirstOrDefault();

                if (cert == null)
                {
                    return null;
                }

                Console.WriteLine($"[Broker] Using the current ASP.NET Core HTTPS development certificate for localhost (thumbprint: {cert.Thumbprint}) - read directly from the OS certificate store, no export/staleness risk.");
                return cert;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Broker] Could not read a trusted localhost certificate from the OS store: {ex.Message} - the localhost-only HTTPS port will not be started.");
                return null;
            }
        }

        private static IPAddress[] GetLocalIPv4Addresses()
        {
            try
            {
                var addresses = new System.Collections.Generic.List<IPAddress>();

                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up)
                    {
                        continue;
                    }

                    foreach (var addrInfo in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (addrInfo.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addrInfo.Address))
                        {
                            addresses.Add(addrInfo.Address);
                        }
                    }
                }

                return addresses.ToArray();
            }
            catch
            {
                return Array.Empty<IPAddress>();
            }
        }
    }
}
