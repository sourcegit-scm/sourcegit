using System.Diagnostics;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class MergeTree : Command
    {
        public MergeTree(string repo, string source, string dest)
        {
            WorkingDirectory = repo;
            Args = $"merge-tree --write-tree {source} {dest}";
        }

        public async Task<int> GetExitCodeAsync()
        {
            using var proc = new Process();
            proc.StartInfo = CreateGitStartInfo(false);

            var exitCode = -1;
            try
            {
                proc.Start();
                await proc.WaitForExitAsync().ConfigureAwait(false);
                exitCode = proc.ExitCode;
            }
            catch
            {
                // Ignore any exceptions and just return -1
            }

            return exitCode;
        }
    }
}
