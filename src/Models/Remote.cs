using System;
using System.IO;
using System.Text.RegularExpressions;

namespace SourceGit.Models
{
    public partial class Remote
    {
        [GeneratedRegex(@"^https?://[^/]+/.+[^/\.]$")]
        private static partial Regex REG_HTTPS();

        [GeneratedRegex(@"^git://[^/]+/.+[^/\.]$")]
        private static partial Regex REG_GIT();

        [GeneratedRegex(@"^[\w\-]+@[\w\.\-]+(\:[0-9]+)?:([a-zA-z0-9~%][\w\-\./~%]*)?[a-zA-Z0-9](\.git)?$")]
        private static partial Regex REG_SSH1();

        [GeneratedRegex(@"^ssh://([\w\-]+@)?[\w\.\-]+(\:[0-9]+)?/([a-zA-z0-9~%][\w\-\./~%]*)?[a-zA-Z0-9](\.git)?$")]
        private static partial Regex REG_SSH2();

        [GeneratedRegex(@"^git@([\w\.\-]+):([\w\.\-/~%]+/[\w\-\.%]+)\.git$")]
        private static partial Regex REG_TO_VISIT_URL_CAPTURE();

        private static readonly Regex[] URL_FORMATS = [
            REG_HTTPS(),
            REG_GIT(),
            REG_SSH1(),
            REG_SSH2(),
        ];

        public string Name { get; set; }
        public string URL { get; set; }

        public static bool IsSSH(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            if (REG_SSH1().IsMatch(url))
                return true;

            return REG_SSH2().IsMatch(url);
        }

        public static bool IsValidURL(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            foreach (var fmt in URL_FORMATS)
            {
                if (fmt.IsMatch(url))
                    return true;
            }

            return url.StartsWith("file://", StringComparison.Ordinal) ||
                url.StartsWith("./", StringComparison.Ordinal) ||
                url.StartsWith("../", StringComparison.Ordinal) ||
                Directory.Exists(url);
        }

        public bool TryGetVisitURL(out string url)
        {
            url = null;

            if (URL.StartsWith("http", StringComparison.Ordinal))
            {
                // Try to remove the user before host and `.git` extension.
                var uri = new Uri(URL.EndsWith(".git", StringComparison.Ordinal) ? URL.Substring(0, URL.Length - 4) : URL);
                if (uri.Port != 80 && uri.Port != 443)
                    url = $"{uri.Scheme}://{uri.Host}:{uri.Port}{uri.LocalPath}";
                else
                    url = $"{uri.Scheme}://{uri.Host}{uri.LocalPath}";

                return true;
            }

            var match = REG_TO_VISIT_URL_CAPTURE().Match(URL);
            if (match.Success)
            {
                url = $"https://{match.Groups[1].Value}/{match.Groups[2].Value}";
                return true;
            }

            return false;
        }
    }
}
