using System.IO;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class Reword : Command
    {
        public Reword(string repo, string message)
        {
            _tmpFile = Path.GetTempFileName();
            _message = message;

            WorkingDirectory = repo;
            Context = repo;
            Args = $"commit --amend --only --allow-empty --file={_tmpFile.Quoted()}";
        }

        public async Task<bool> RunAsync()
        {
            try
            {
                await File.WriteAllTextAsync(_tmpFile, _message).ConfigureAwait(false);
                var succ = await ExecAsync().ConfigureAwait(false);
                File.Delete(_tmpFile);
                return succ;
            }
            catch
            {
                return false;
            }
        }

        private readonly string _tmpFile;
        private readonly string _message;
    }
}
