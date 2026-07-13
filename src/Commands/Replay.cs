using System.Diagnostics;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class Replay : Command
    {
        public Replay(string repo, string onto, string range)
        {
            WorkingDirectory = repo;
            Context = repo;
            RaiseError = false;
            Args = $"replay --onto {onto} {range}";
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
