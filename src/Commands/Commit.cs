using System.IO;

namespace SourceGit.Commands
{
    public class Commit : Command
    {
        public Commit(string repo, string message, bool amend)
        {
            var file = Path.GetTempFileName();
            File.WriteAllText(file, message);

            WorkingDirectory = repo;
            Context = repo;
            TraitErrorAsOutput = true;
            Args = $"commit --allow-empty --file=\"{file}\"";
            if (amend)
                Args += " --amend --no-edit";
        }
    }
}
