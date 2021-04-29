namespace SourceGit.Commands {
    /// <summary>
    ///     config命令
    /// </summary>
    public class Config : Command {

        public Config() {
        }

        public Config(string repo) {
            Cwd = repo;
        }

        public string Get(string key) {
            Args = $"config {key}";
            return ReadToEnd().Output.Trim();
        }

        public bool Set(string key, string val, bool allowEmpty = false) {
            if (!allowEmpty && string.IsNullOrEmpty(val)) {
                if (string.IsNullOrEmpty(Cwd)) {
                    Args = $"config --global --unset {key}";
                } else {
                    Args = $"config --unset {key}";
                }
            } else {
                if (string.IsNullOrEmpty(Cwd)) {
                    Args = $"config --global {key} \"{val}\"";
                } else {
                    Args = $"config {key} \"{val}\"";
                }
            }

            return Exec();
        }
    }
}
