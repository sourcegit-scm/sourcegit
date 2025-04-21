using System;
using System.Collections.Generic;
using System.Linq;

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
        /// Attempts to create a CommitLink for a given remote by matching a provider.
        /// </summary>
        private static CommitLink? TryCreateCommitLink(Remote remote)
        {
            if (!remote.TryGetVisitURL(out var url))
                return null;
            var provider = Providers.FirstOrDefault(p => p.IsMatch(url));
            if (provider.Name == null)
                return null;
            string repoName = provider.ExtractRepo(url);
            return new CommitLink($"{provider.Name} ({repoName})", provider.BuildCommitUrlPrefix(url));
        }

        /// <summary>
        /// Translates remotes to CommitLinks. TODO: rename
        /// </summary>
        public static List<CommitLink> Get(List<Remote> remotes)
        {
            return remotes
                .Select(TryCreateCommitLink)
                .Where(cl => cl != null)
                .Cast<CommitLink>()
                .ToList();
        }
    }
}
