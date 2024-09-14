using System.IO;

namespace SourceGit.Commands
{
    public class Commit : Command
    {
        public Commit(string repo, string message, bool amend, bool allowEmpty = false)
        {
            var file = Path.GetTempFileName();
            File.WriteAllText(file, message);

            WorkingDirectory = repo;
            Context = repo;
            TraitErrorAsOutput = true;
            Args = ["commit", $"--file={file}"];
            if (amend)
                Args.AddRange(["--amend", "--no-edit"]);
            if (allowEmpty)
                Args.Add("--allow-empty");
        }
    }
}
