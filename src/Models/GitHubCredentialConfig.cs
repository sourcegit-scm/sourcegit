using System;
using System.Collections.Generic;

namespace SourceGit.Models
{
    public static class GitHubCredentialConfig
    {
        public static List<string> GetUsernameKeys(IEnumerable<string> remoteUrls)
        {
            var keys = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var remoteUrl in remoteUrls)
            {
                if (!Uri.TryCreate(remoteUrl, UriKind.Absolute, out var uri))
                    continue;

                if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
                    !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
                    continue;

                var key = $"credential.{uri.Scheme.ToLowerInvariant()}://github.com.username";
                if (seen.Add(key))
                    keys.Add(key);
            }

            return keys;
        }
    }
}
