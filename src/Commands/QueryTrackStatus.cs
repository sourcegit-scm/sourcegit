using System;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryTrackStatus : Command
    {
        public QueryTrackStatus(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
        }

        public async Task GetResultAsync(Models.Branch local, Models.Branch remote)
        {
            Args = $"rev-list --left-right {local.Head}...{remote.Head}";

            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return;

            var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line[0] == '>')
                    local.Behind.Add(line.Substring(1));
                else
                    local.Ahead.Add(line.Substring(1));
            }
        }
    }
}
