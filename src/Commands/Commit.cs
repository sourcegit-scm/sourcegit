using System.IO;

namespace SourceGit.Commands
{
    public class Commit : Command
    {
        public Commit(string repo, string message, bool amend, bool signOff)
        {
            _tmpFile = Path.GetTempFileName();
            File.WriteAllText(_tmpFile, message);

            WorkingDirectory = repo;
            Context = repo;
            TraitErrorAsOutput = true;
            Args = $"commit --allow-empty --file=\"{_tmpFile}\"";
            if (amend)
                Args += " --amend --no-edit";
            if (signOff)
                Args += " --signoff";
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

        private string _tmpFile = string.Empty;
    }
}
