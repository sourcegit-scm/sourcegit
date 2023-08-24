using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SourceGit.Commands {
    /// <summary>
    ///     查看、添加或移除忽略变更文件
    /// </summary>
    public class AssumeUnchanged {
        private string repo;

        class ViewCommand : Command {
            private static readonly Regex REG = new Regex(@"^(\w)\s+(.+)$");
            private List<string> outs = new List<string>();

            public ViewCommand(string repo) {
                Cwd = repo;
                Args = "ls-files -v";
            }

            public List<string> Result() {
                Exec();
                return outs;
            }

            public override void OnReadline(string line) {
                var match = REG.Match(line);
                if (!match.Success) return;

                if (match.Groups[1].Value == "h") {
                    outs.Add(match.Groups[2].Value);
                }
            }
        }

        class ModCommand : Command {
            public ModCommand(string repo, string file, bool bAdd) {
                var mode = bAdd ? "--assume-unchanged" : "--no-assume-unchanged";

                Cwd = repo;
                Args = $"update-index {mode} -- \"{file}\"";
            }
        }

        public AssumeUnchanged(string repo) {
            this.repo = repo;
        }

        public List<string> View() {
            return new ViewCommand(repo).Result();
        }

        public void Add(string file) {
            new ModCommand(repo, file, true).Exec();
        }

        public void Remove(string file) {
            new ModCommand(repo, file, false).Exec();
        }
    }
}
