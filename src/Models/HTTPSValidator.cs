using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.Models
{
    public static class HTTPSValidator
    {
        public static void Add(string host)
        {
            lock (_syncLock)
            {
                // Already checked
                if (_hosts.ContainsKey(host))
                    return;

                // Temporarily mark as supported to avoid duplicate checks
                _hosts.Add(host, true);

                // Well-known hosts always support HTTPS
                if (host.Contains("github.com", StringComparison.Ordinal) ||
                    host.Contains("gitlab", StringComparison.Ordinal) ||
                    host.Contains("azure.com", StringComparison.Ordinal) ||
                    host.Equals("gitee.com", StringComparison.Ordinal) ||
                    host.Equals("bitbucket.org", StringComparison.Ordinal) ||
                    host.Equals("gitea.org", StringComparison.Ordinal) ||
                    host.Equals("gitcode.com", StringComparison.Ordinal))
                    return;
            }

            Task.Run(() =>
            {
                var supported = false;

                try
                {
                    using (var client = new TcpClient())
                    {
                        client.ConnectAsync(host, 443).Wait(3000);
                        if (!client.Connected)
                        {
                            client.ConnectAsync(host, 80).Wait(3000);
                            supported = !client.Connected; // If the network is not available, assume HTTPS is supported
                        }
                        else
                        {
                            using (var ssl = new SslStream(client.GetStream(), false, (s, cert, chain, errs) => true))
                            {
                                ssl.AuthenticateAsClient(host);
                                supported = ssl.IsAuthenticated; // Hand-shake succeeded
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore exceptions
                }

                lock (_syncLock)
                {
                    _hosts[host] = supported;
                }
            });
        }

        public static bool IsSupported(string host)
        {
            lock (_syncLock)
            {
                if (_hosts.TryGetValue(host, out var supported))
                    return supported;

                return false;
            }
        }

        private static Lock _syncLock = new();
        private static Dictionary<string, bool> _hosts = new();
    }
}
