using System.IO;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class Commit : Command
    {
        public Commit(string repo, string message, bool signOff, bool amend, bool resetAuthor)
        {
            _tmpFile = Path.GetTempFileName();
            File.WriteAllText(_tmpFile, message);

            WorkingDirectory = repo;
            Context = repo;
            Args = $"commit --allow-empty --file=\"{_tmpFile}\"";
            if (signOff)
                Args += " --signoff";
            if (amend)
                Args += resetAuthor ? " --amend --reset-author --no-edit" : " --amend --no-edit";
        }

        public bool Run()
        {
            var succ = Exec();

            try
            {
                File.Delete(_tmpFile);
            }
            catch
            {
                // Ignore
            }

            return succ;
        }

        public async Task<bool> RunAsync()
        {
            var succ = await ExecAsync();

            try
            {
                File.Delete(_tmpFile);
            }
            catch
            {
                // Ignore
            }

            return succ;
        }

        private readonly string _tmpFile;
    }
}
