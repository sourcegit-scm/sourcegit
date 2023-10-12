using System;
using System.Collections.Generic;

namespace SourceGit.Commands {
    /// <summary>
    ///     解析所有的Tags
    /// </summary>
    public class Tags : Command {
        public static readonly string CMD = "for-each-ref --sort=-creatordate --format=\"$%(refname:short)$%(objectname)$%(*objectname)\" refs/tags";
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
            var subs = line.Split(new char[] { '$' }, StringSplitOptions.RemoveEmptyEntries);
            if (subs.Length == 2) {
                loaded.Add(new Models.Tag() {
                    Name = subs[0],
                    SHA = subs[1],
                });
            } else if (subs.Length == 3) {
                loaded.Add(new Models.Tag() {
                    Name = subs[0],
                    SHA = subs[2],
                });
            }
        }
    }
}
