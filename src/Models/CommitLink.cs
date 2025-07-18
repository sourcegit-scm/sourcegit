using System;
using System.Collections.Generic;

namespace SourceGit.Models
{
    public class CommitLink
    {
        public string Name { get; set; }
        public string URLPrefix { get; set; }

        public CommitLink(string name, string prefix)
        {
            Name = name;
            URLPrefix = prefix;
        }

        public static List<CommitLink> Get(List<Remote> remotes)
        {
            var outs = new List<CommitLink>();

            foreach (var remote in remotes)
            {
                if (remote.TryGetVisitURL(out var url))
                {
                    var trimmedUrl = url.AsSpan();
                    if (url.EndsWith(".git"))
                        trimmedUrl = url.AsSpan(0, url.Length - 4);

                    if (url.StartsWith("https://github.com/", StringComparison.Ordinal))
                        outs.Add(new($"GitHub ({trimmedUrl[19..]})", $"{url}/commit/"));
                    else if (url.StartsWith("https://gitlab.", StringComparison.Ordinal))
                        outs.Add(new($"GitLab ({trimmedUrl[(trimmedUrl[15..].IndexOf('/') + 16)..]})", $"{url}/-/commit/"));
                    else if (url.StartsWith("https://gitee.com/", StringComparison.Ordinal))
                        outs.Add(new($"Gitee ({trimmedUrl[18..]})", $"{url}/commit/"));
                    else if (url.StartsWith("https://bitbucket.org/", StringComparison.Ordinal))
                        outs.Add(new($"BitBucket ({trimmedUrl[22..]})", $"{url}/commits/"));
                    else if (url.StartsWith("https://codeberg.org/", StringComparison.Ordinal))
                        outs.Add(new($"Codeberg ({trimmedUrl[21..]})", $"{url}/commit/"));
                    else if (url.StartsWith("https://gitea.org/", StringComparison.Ordinal))
                        outs.Add(new($"Gitea ({trimmedUrl[18..]})", $"{url}/commit/"));
                    else if (url.StartsWith("https://git.sr.ht/", StringComparison.Ordinal))
                        outs.Add(new($"sourcehut ({trimmedUrl[18..]})", $"{url}/commit/"));
                }
            }

            return outs;
        }
    }
}
