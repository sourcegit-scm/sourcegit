using System;

namespace SourceGit.Commands {
    /// <summary>
    ///     取得一个LFS对象的信息
    /// </summary>
    public class QueryLFSObject : Command {
        private Models.LFSObject obj = new Models.LFSObject();

        public QueryLFSObject(string repo, string commit, string path) {
            Cwd = repo;
            Args = $"show {commit}:\"{path}\"";
        }

        public Models.LFSObject Result() {
            Exec();
            return obj;
        }

        public override void OnReadline(string line) {
            if (line.StartsWith("oid sha256:", StringComparison.Ordinal)) {
                obj.OID = line.Substring(11).Trim();
            } else if (line.StartsWith("size")) {
                obj.Size = int.Parse(line.Substring(4).Trim());
            }
        }
    }
}
