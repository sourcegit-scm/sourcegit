using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryUnstagedFiles : Command
    {
        public QueryUnstagedFiles(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"log origin/develop..HEAD --name-only --pretty=format:";
        }

        public async Task<List<string>> GetResultAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            var outs = new List<string>();
            if (rs.IsSuccess)
            {
                var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    outs.Add(line);
                }
            }

            return outs;
        }
    }
}
