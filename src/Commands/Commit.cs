using System.IO;

namespace SourceGit.Commands {
    /// <summary>
    ///     `git commit`命令
    /// </summary>
    public class Commit : Command {
        public Commit(string repo, string message, bool amend) {
            var file = Path.GetTempFileName();
            File.WriteAllText(file, message);

            Cwd = repo;
            Args = $"commit --file=\"{file}\"";
            if (amend) Args += " --amend --no-edit";
        }
    }
}
