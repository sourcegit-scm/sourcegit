using System;

namespace SourceGit.Commands
{
    public class CountLocalChangesWithoutUntracked : Command
    {
        public CountLocalChangesWithoutUntracked(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = "--no-optional-locks status -uno --ignore-submodules=all --porcelain";
        }

        public int Result()
        {
            var rs = ReadToEnd();
            if (rs.IsSuccess)
            {
                var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                return lines.Length;
            }

            return 0;
        }
    }
}
