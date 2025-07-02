﻿using System;
using System.Threading.Tasks;

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

        public async Task<int> ResultAsync()
        {
            var rs = await ReadToEndAsync();
            if (rs.IsSuccess)
            {
                var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                return lines.Length;
            }

            return 0;
        }
    }
}
