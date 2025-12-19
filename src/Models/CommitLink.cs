using System;
using System.Collections.Generic;

namespace SourceGit.Models
{
    public class CommitLink
    {
        public string Name { get; } = null;
        public string URLPrefix { get; } = null;

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
                if (remote.TryGetVisitURL(out var link))
                {
                    var uri = new Uri(link, UriKind.Absolute);
                    var host = uri.Host;
                    var route = uri.AbsolutePath.TrimStart('/');

                    if (host.Equals("github.com", StringComparison.Ordinal))
                        outs.Add(new($"GitHub ({route})", $"{link}/commit/"));
                    else if (host.Contains("gitlab", StringComparison.Ordinal))
                        outs.Add(new($"GitLab ({route})", $"{link}/-/commit/"));
                    else if (host.Equals("gitee.com", StringComparison.Ordinal))
                        outs.Add(new($"Gitee ({route})", $"{link}/commit/"));
                    else if (host.Equals("bitbucket.org", StringComparison.Ordinal))
                        outs.Add(new($"BitBucket ({route})", $"{link}/commits/"));
                    else if (host.Equals("codeberg.org", StringComparison.Ordinal))
                        outs.Add(new($"Codeberg ({route})", $"{link}/commit/"));
                    else if (host.Equals("gitea.org", StringComparison.Ordinal))
                        outs.Add(new($"Gitea ({route})", $"{link}/commit/"));
                    else if (host.Equals("git.sr.ht", StringComparison.Ordinal))
                        outs.Add(new($"sourcehut ({route})", $"{link}/commit/"));
                    else if (host.Equals("gitcode.com", StringComparison.Ordinal))
                        outs.Add(new($"GitCode ({route})", $"{link}/commit/"));
                }
            }

            return outs;
        }
    }
}
