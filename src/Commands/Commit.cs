using System.IO;

namespace SourceGit.Commands {
    /// <summary>
    ///     `git commit`命令
    /// </summary>
    public class Commit : Command {
        private string msg = null;

        public Commit(string repo, string message, bool amend) {
            msg = Path.GetTempFileName();
            File.WriteAllText(msg, message);

            Cwd = repo;
            Args = $"commit --file=\"{msg}\"";
            if (amend) Args += " --amend --no-edit";
        }
    }
}
