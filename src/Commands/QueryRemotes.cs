using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public partial class QueryRemotes : Command
    {
        [GeneratedRegex(@"^([\w\.\-]+)\s*(\S+).*$")]
        private static partial Regex REG_REMOTE();

        public QueryRemotes(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = "remote -v";
        }

        public async Task<List<Models.Remote>> GetResultAsync()
        {
            var outs = new List<Models.Remote>();
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return outs;

            var config = await new Config(WorkingDirectory).ReadAllAsync().ConfigureAwait(false);
            var disableAutoFetchRemotes = new HashSet<string>();
            foreach (var (k, v) in config)
            {
                if (k.StartsWith("remote.", StringComparison.Ordinal) &&
                    k.EndsWith(".disableautofetch", StringComparison.Ordinal) &&
                    v.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    var remoteName = k.Substring(7, k.Length - 24).Trim('"');
                    disableAutoFetchRemotes.Add(remoteName);
                }
            }

            var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var match = REG_REMOTE().Match(line);
                if (!match.Success)
                    continue;

                var remote = new Models.Remote()
                {
                    Name = match.Groups[1].Value,
                    URL = match.Groups[2].Value,
                    DisableAutoFetch = disableAutoFetchRemotes.Contains(match.Groups[1].Value)
                };

                if (outs.Find(x => x.Name == remote.Name) != null)
                    continue;

                if (remote.URL.StartsWith("git@", StringComparison.Ordinal))
                {
                    var hostEnd = remote.URL.IndexOf(':', 4);
                    if (hostEnd > 4)
                    {
                        var host = remote.URL.Substring(4, hostEnd - 4);
                        Models.HTTPSValidator.Add(host);
                    }
                }

                outs.Add(remote);
            }

            return outs;
        }
    }
}
