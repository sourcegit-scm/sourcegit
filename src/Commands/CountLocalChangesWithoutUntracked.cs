using System;

namespace SourceGit.Commands
{
    public class CountLocalChangesWithoutUntracked : Command
    {
        public CountLocalChangesWithoutUntracked(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = "--no-optional-locks status -uno --ignore-submodules=dirty --porcelain";
        }

        public int Result()
        {
            var rs = ReadToEnd();
            if (rs.IsSuccess)
            {
                var lines = rs.StdOut.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
                return lines.Length;
            }

            return 0;
        }
    }
}
