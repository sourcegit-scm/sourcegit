using System;
using System.IO;
using System.Security.Cryptography;

namespace SourceGit.Models
{
    public class SSHKeyPair
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public string PublicKey { get; set; }
        public string Fingerprint { get; set; } = "--- (invalid)";

        public SSHKeyPair(string file)
        {
            Name = Path.GetFileName(file);
            FullPath = file;
            PublicKey = File.ReadAllText($"{file}.pub");

            var parts = PublicKey.Split(' ', StringSplitOptions.RemoveEmptyEntries);
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
