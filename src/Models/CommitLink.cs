using System;
using System.Collections.Generic;

namespace SourceGit.Models
{
    /// <summary>
    /// Represents a commit link for a remote repository.
    /// </summary>
    public class CommitLink
    {
        public string Name { get; set; }
        public string URLPrefix { get; set; }

        public CommitLink(string name, string prefix)
        {
            Name = name;
            URLPrefix = prefix;
        }

        public readonly record struct ProviderInfo(string Host, string Display, string CommitPath, int NameStart);

        private static readonly ProviderInfo[] Providers = new[]
        {
            new ProviderInfo("https://github.com/", "Github", "/commit/", 19),
            new ProviderInfo("https://gitlab.", "GitLab", "/-/commit/", 15),
            new ProviderInfo("https://gitee.com/", "Gitee", "/commit/", 18),
            new ProviderInfo("https://bitbucket.org/", "BitBucket", "/commits/", 22),
            new ProviderInfo("https://codeberg.org/", "Codeberg", "/commit/", 21),
            new ProviderInfo("https://gitea.org/", "Gitea", "/commit/", 18),
            new ProviderInfo("https://git.sr.ht/", "sourcehut", "/commit/", 18)
        };

        public static List<CommitLink> Get(List<Remote> remotes)
        {
            var outs = new List<CommitLink>();

            foreach (var remote in remotes)
            {
                if (remote.TryGetVisitURL(out var url))
                {
                    var trimmedUrl = url.EndsWith(".git") ? url[..^4] : url;
                    foreach (var provider in Providers)
                    {
                        if (url.StartsWith(provider.Host, StringComparison.Ordinal))
                        {
                            string repoName;
                            if (provider.Host == "https://gitlab.")
                            {
                                // GitLab: find the first '/' after host
                                int idx = trimmedUrl[provider.NameStart..].IndexOf('/') + provider.NameStart + 1;
                                repoName = trimmedUrl[idx..];
                            }
                            else
                            {
                                repoName = trimmedUrl[provider.NameStart..];
                            }
                            outs.Add(new CommitLink($"{provider.Display} ({repoName})", $"{url}{provider.CommitPath}"));
                            break;
                        }
                    }
                }
            }

            return outs;
        }
    }
}
