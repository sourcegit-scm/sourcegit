using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

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
            return remotes.Select(remote =>
            {

                var rr = TryCreateCommitLink(remote);
#if DEBUG
                /// Inplace Test

                if (remote.TryGetVisitURL(out var url))
                {
                    var commitLink = GetCommitLinkOriginalImplementionForTestPurposes(url);
                    Debug.Assert(commitLink == rr, " checking comparing with initial implementation failed, TODO: delete in future");
                }
#endif
                return rr;
            }).Where(cl => cl != null).ToList();
        }

#if DEBUG
        // Minimal stub for Remote for testing


        // TODO : delete this after checking the implementation
        private static CommitLink? GetCommitLinkOriginalImplementionForTestPurposes(string url)
        {
            var outs = new List<CommitLink>();

            var trimmedUrl = url;
            if (url.EndsWith(".git"))
                trimmedUrl = url.Substring(0, url.Length - 4);

            if (url.StartsWith("https://github.com/", StringComparison.Ordinal))
                outs.Add(new($"Github ({trimmedUrl.Substring(19)})", $"{url}/commit/"));
            else if (url.StartsWith("https://gitlab.", StringComparison.Ordinal))
                outs.Add(new($"GitLab ({trimmedUrl.Substring(trimmedUrl.Substring(15).IndexOf('/') + 16)})", $"{url}/-/commit/"));
            else if (url.StartsWith("https://gitee.com/", StringComparison.Ordinal))
                outs.Add(new($"Gitee ({trimmedUrl.Substring(18)})", $"{url}/commit/"));
            else if (url.StartsWith("https://bitbucket.org/", StringComparison.Ordinal))
                outs.Add(new($"BitBucket ({trimmedUrl.Substring(22)})", $"{url}/commits/"));
            else if (url.StartsWith("https://codeberg.org/", StringComparison.Ordinal))
                outs.Add(new($"Codeberg ({trimmedUrl.Substring(21)})", $"{url}/commit/"));
            else if (url.StartsWith("https://gitea.org/", StringComparison.Ordinal))
                outs.Add(new($"Gitea ({trimmedUrl.Substring(18)})", $"{url}/commit/"));
            else if (url.StartsWith("https://git.sr.ht/", StringComparison.Ordinal))
                outs.Add(new($"sourcehut ({trimmedUrl.Substring(18)})", $"{url}/commit/"));

            return outs.FirstOrDefault();
        }
        static CommitLink()
        {

            //Unit tests , TODO: make normal UnitTests, delete this code.
            // Test Github
            var githubRemote = new Remote() { URL = "https://github.com/user/repo.git" };
            var links = Get(new List<Remote> { githubRemote });
            Debug.Assert(links.Count == 1, "Should find one CommitLink for Github");
            Debug.Assert(links[0].Name.StartsWith("Github"), "Provider should be Github");
            Debug.Assert(links[0].URLPrefix == "https://github.com/user/repo/commit/", "URLPrefix should be correct for Github");

            // Test BitBucket
            var bitbucketRemote = new Remote() { URL = "https://bitbucket.org/team/project" };
            links = Get(new List<Remote> { bitbucketRemote });
            Debug.Assert(links.Count == 1, "Should find one CommitLink for BitBucket");
            Debug.Assert(links[0].Name.StartsWith("BitBucket"), "Provider should be BitBucket");
            Debug.Assert(links[0].URLPrefix == "https://bitbucket.org/team/project/commits/", "URLPrefix should be correct for BitBucket");
        }
#endif
    }
}
