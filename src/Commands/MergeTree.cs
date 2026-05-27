using System;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class MergeTree : Command
    {
        public MergeTree(string repo, string baseTree, string source, string dest)
        {
            WorkingDirectory = repo;
            Context = repo;
            RaiseError = false;
            Args = $"merge-tree {baseTree} {source} {dest}";
        }

        public async Task<bool> CheckAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return false;

            var stdout = rs.StdOut;
            return !stdout.Contains("\n+>>>>>>>", StringComparison.Ordinal) &&
                !stdout.Contains("\n+<<<<<<<", StringComparison.Ordinal) &&
                !stdout.Contains("\n->>>>>>>", StringComparison.Ordinal) &&
                !stdout.Contains("\n-<<<<<<<", StringComparison.Ordinal);
        }
    }
}
