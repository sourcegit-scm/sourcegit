using System.IO;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class Commit : Command
    {
        public Commit(string repo, string message, bool signOff, bool amend, bool resetAuthor)
        {
            _tmpFile = Path.GetTempFileName();
            _message = message;

            WorkingDirectory = repo;
            Context = repo;
            Args = $"commit --allow-empty --file={_tmpFile.Quoted()}";
            if (signOff)
                Args += " --signoff";
            if (amend)
                Args += resetAuthor ? " --amend --reset-author --no-edit" : " --amend --no-edit";
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
