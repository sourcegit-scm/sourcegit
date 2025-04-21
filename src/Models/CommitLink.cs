using System;
using System.Collections.Generic;

namespace SourceGit.Models
{
    public class CommitLink
    {
        public string Name { get; set; } = null;
        public string URLPrefix { get; set; } = null;

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
                }
            }

            return outs;
        }
    }
}
