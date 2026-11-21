using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    /// <summary>
    ///     Queries the directories that are entirely ignored by git (they match an ignore rule and contain no tracked
    ///     files). Paths are relative to the repository root, use '/' as separator and end with '/'.
    /// </summary>
    public class QueryIgnoredDirectories : Command
    {
        public QueryIgnoredDirectories(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            RaiseError = false;
            Args = "--no-optional-locks status --porcelain -z --ignored=matching --untracked-files=normal --ignore-submodules=all --no-renames";
        }

        public async Task<HashSet<string>> GetResultAsync()
        {
            var outs = new HashSet<string>(StringComparer.Ordinal);
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return outs;

            var entries = rs.StdOut.Split('\0', StringSplitOptions.RemoveEmptyEntries);
            foreach (var entry in entries)
            {
                if (entry.Length > 4 && entry.StartsWith("!! ", StringComparison.Ordinal) && entry.EndsWith('/'))
                    outs.Add(entry.Substring(3));
            }

            return outs;
        }
    }
}
