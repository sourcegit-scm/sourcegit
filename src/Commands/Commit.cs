using System.IO;
using System.Text;
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

            var builder = new StringBuilder("commit --allow-empty --file=").Append(_tmpFile.Quoted());
            if (signOff)
                builder.Append(" --signoff");
            if (amend)
            {
                builder.Append(" --amend --no-edit");
                if (resetAuthor)
                    builder.Append(" --reset-author");
            }
            Args = builder.ToString();
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

        private readonly string _tmpFile = string.Empty;
        private readonly string _message = string.Empty;
    }
}
