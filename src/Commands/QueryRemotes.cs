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
                };

                if (outs.Find(x => x.Name == remote.Name) != null)
                    continue;

                outs.Add(remote);
            }

            return outs;
        }
    }
}
