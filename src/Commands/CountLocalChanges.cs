using System;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class CountLocalChanges : Command
    {
        public CountLocalChanges(string repo, bool includeUntracked)
        {
            var option = includeUntracked ? "-uall" : "-uno";
            WorkingDirectory = repo;
            Context = repo;
            Args = $"--no-optional-locks status {option} --ignore-submodules=all --porcelain";
        }

        public async Task<int> GetResultAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (rs.IsSuccess)
            {
                var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                return lines.Length;
            }

            return 0;
        }
    }
}
