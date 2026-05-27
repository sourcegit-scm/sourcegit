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

        public async Task<bool> CheckAsync()
        {
            using var proc = new Process();
            proc.StartInfo = CreateGitStartInfo(false);

            try
            {
                proc.Start();
                await proc.WaitForExitAsync().ConfigureAwait(false);
                return proc.ExitCode == 0;
            }
            catch
            {
                // Ignore any exceptions and just return false
                return false;
            }
        }
    }
}
