using System.IO;

namespace SourceGit.Commands {

    /// <summary>
    ///     标签相关指令
    /// </summary>
    public class Tag : Command {

        public Tag(string repo) {
            Cwd = repo;
        }

        public bool Add(string name, string basedOn, string message) {
            Args = $"tag -a {name} {basedOn} ";

            if (!string.IsNullOrEmpty(message)) {
                string tmp = Path.GetTempFileName();
                File.WriteAllText(tmp, message);
                Args += $"-F \"{tmp}\"";
            } else {
                Args += $"-m {name}";
            }

            return Exec();
        }

        public bool Delete(string name, bool push) {
            Args = $"tag --delete {name}";
            if (!Exec()) return false;

            var repo = Models.Preference.Instance.FindRepository(Cwd);
            if (repo != null && repo.Filters.Contains(name)) {
                repo.Filters.Remove(name);
            }

            if (push) {
                var remotes = new Remotes(Cwd).Result();
                foreach (var r in remotes) {
                    new Push(Cwd, r.Name, name, true).Exec();
                }
            }

            return true;
        }
    }
}
