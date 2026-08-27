using System;
using System.IO;
using System.Security.Cryptography;

namespace SourceGit.Models
{
    public class SSHKeyPair
    {
        public string PrivateKeyPath { get; set; }
        public string PublicKeyPath { get; set; }
        public string RawPublicKey { get; set; }
        public string Fingerprint { get; set; } = "--- (invalid)";
        public string Name => Path.GetFileName(PrivateKeyPath);

        public SSHKeyPair(string privateKey, string publicKey)
        {
            PrivateKeyPath = privateKey;
            PublicKeyPath = publicKey;
            RawPublicKey = File.ReadAllText(publicKey);

            var parts = RawPublicKey.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
                return;

            try
            {
                var blob = Convert.FromBase64String(parts[1]);
                Fingerprint = "SHA256:" + Convert.ToBase64String(SHA256.HashData(blob)).TrimEnd('=');
            }
            catch
            {
                // Ignore errors and keep the fingerprint as invalid
            }
        }
    }
}
