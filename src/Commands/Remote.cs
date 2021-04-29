namespace SourceGit.Commands {
    /// <summary>
    ///     远程操作
    /// </summary>
    public class Remote : Command {

        public Remote(string repo) {
            Cwd = repo;
        }

        public bool Add(string name, string url) {
            Args = $"remote add {name} {url}";
            return Exec();
        }

        public bool Delete(string name) {
            Args = $"remote remove {name}";
            return Exec();
        }

        public bool Rename(string name, string to) {
            Args = $"remote rename {name} {to}";
            return Exec();
        }

        public bool SetURL(string name, string url) {
            Args = $"remote set-url {name} {url}";
            return Exec();
        }
    }
}
