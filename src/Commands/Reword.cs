using System.IO;

namespace SourceGit.Commands {
    /// <summary>
    ///     编辑HEAD的提交信息
    /// </summary>
    public class Reword : Command {
        public Reword(string repo, string msg) {
            var tmp = Path.GetTempFileName();
            File.WriteAllText(tmp, msg);

            Cwd = repo;
            Args = $"commit --amend --allow-empty --file=\"{tmp}\"";
        }
    }
}
