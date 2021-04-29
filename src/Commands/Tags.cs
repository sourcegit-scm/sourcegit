using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SourceGit.Commands {
    /// <summary>
    ///     解析所有的Tags
    /// </summary>
    public class Tags : Command {
        public static readonly string CMD = "for-each-ref --sort=-creatordate --format=\"$%(refname:short)$%(objectname)$%(*objectname)\" refs/tags";
        public static readonly Regex REG_FORMAT = new Regex(@"\$(.*)\$(.*)\$(.*)");

        private List<Models.Tag> loaded = new List<Models.Tag>();

        public Tags(string path) {
            Cwd = path;
            Args = CMD;
        }

        public List<Models.Tag> Result() {
            Exec();
            return loaded;
        }

        public override void OnReadline(string line) {
            var match = REG_FORMAT.Match(line);
            if (!match.Success) return;

            var name = match.Groups[1].Value;
            var commit = match.Groups[2].Value;
            var dereference = match.Groups[3].Value;

            if (string.IsNullOrEmpty(dereference)) {
                loaded.Add(new Models.Tag() {
                    Name = name,
                    SHA = commit,
                });
            } else {
                loaded.Add(new Models.Tag() {
                    Name = name,
                    SHA = dereference,
                });
            }
        }
    }
}
