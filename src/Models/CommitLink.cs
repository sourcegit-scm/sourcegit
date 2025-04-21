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

        public readonly record struct ProviderInfo(
            string Name,
            string HostPrefix,
            Func<string, string> ExtractRepo,
            Func<string, string> BuildCommitUrlPrefix)
        {
            public bool IsMatch(string url) => url.StartsWith(HostPrefix, StringComparison.Ordinal);
        }

        private static readonly ProviderInfo[] Providers = new[]
        {
            new ProviderInfo(
                "Github",
                "https://github.com/",
                url => url.EndsWith(".git") ? url[19..^4] : url[19..],
                baseUrl => $"{baseUrl}/commit/"
            ),
            new ProviderInfo(
                "GitLab",
                "https://gitlab.",
                url => {
                    var trimmed = url.EndsWith(".git") ? url[15..^4] : url[15..];
                    int idx = trimmed.IndexOf('/') + 1;
                    return trimmed[idx..];
                },
                baseUrl => $"{baseUrl}/-/commit/"
            ),
            new ProviderInfo(
                "Gitee",
                "https://gitee.com/",
                url => url.EndsWith(".git") ? url[18..^4] : url[18..],
                baseUrl => $"{baseUrl}/commit/"
            ),
            new ProviderInfo(
                "BitBucket",
                "https://bitbucket.org/",
                url => url.EndsWith(".git") ? url[22..^4] : url[22..],
                baseUrl => $"{baseUrl}/commits/"
            ),
            new ProviderInfo(
                "Codeberg",
                "https://codeberg.org/",
                url => url.EndsWith(".git") ? url[21..^4] : url[21..],
                baseUrl => $"{baseUrl}/commit/"
            ),
            new ProviderInfo(
                "Gitea",
                "https://gitea.org/",
                url => url.EndsWith(".git") ? url[18..^4] : url[18..],
                baseUrl => $"{baseUrl}/commit/"
            ),
            new ProviderInfo(
                "sourcehut",
                "https://git.sr.ht/",
                url => url.EndsWith(".git") ? url[18..^4] : url[18..],
                baseUrl => $"{baseUrl}/commit/"
            )
        };
    /// <summary>
    /// translate remotes to CommitLink-s . TODO: rename
    /// </summary>
    /// <param name="remotes"></param>
    /// <returns></returns>
        public static List<CommitLink> Get(List<Remote> remotes)
        {
            var outs = new List<CommitLink>();

            foreach (var remote in remotes)
            {
                if (remote.TryGetVisitURL(out var url))
                {
                    foreach (var provider in Providers)
                    {
                        if (provider.IsMatch(url))
                        {
                            string repoName = provider.ExtractRepo(url);
                            outs.Add(new CommitLink($"{provider.Name} ({repoName})", provider.BuildCommitUrlPrefix(url)));
                            break;
                        }
                    }
                }
            }

            return outs;
        }
    }
}
