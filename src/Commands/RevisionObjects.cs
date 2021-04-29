using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SourceGit.Commands {
    /// <summary>
    ///     取出指定Revision下的文件列表
    /// </summary>
    public class RevisionObjects : Command {
        private static readonly Regex REG_FORMAT = new Regex(@"^\d+\s+(\w+)\s+([0-9a-f]+)\s+(.*)$");
        private List<Models.Object> objects = new List<Models.Object>();

        public RevisionObjects(string cwd, string sha) {
            Cwd = cwd;
            Args = $"ls-tree -r {sha}";
        }

        public List<Models.Object> Result() {
            Exec();
            return objects;
        }

        public override void OnReadline(string line) {
            var match = REG_FORMAT.Match(line);
            if (!match.Success) return;

            var obj = new Models.Object();
            obj.SHA = match.Groups[2].Value;
            obj.Type = Models.ObjectType.Blob;
            obj.Path = match.Groups[3].Value;

            switch (match.Groups[1].Value) {
            case "blob": obj.Type = Models.ObjectType.Blob; break;
            case "tree": obj.Type = Models.ObjectType.Tree; break;
            case "tag": obj.Type = Models.ObjectType.Tag; break;
            case "commit": obj.Type = Models.ObjectType.Commit; break;
            }

            objects.Add(obj);
        }
    }
}
