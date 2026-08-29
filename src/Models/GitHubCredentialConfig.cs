using System;
using System.Collections.Generic;

namespace SourceGit.Models
{
    public static class GitHubCredentialConfig
    {
        public const string HttpsUsernameKey = "credential.https://github.com.username";
        public const string HttpUsernameKey = "credential.http://github.com.username";

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

                string key;
                if (uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
                    key = HttpsUsernameKey;
                else if (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
                    key = HttpUsernameKey;
                else
                    continue;

                if (seen.Add(key))
                    keys.Add(key);
            }

            return keys;
        }
    }
}
