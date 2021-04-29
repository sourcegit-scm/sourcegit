namespace SourceGit.Commands {
    /// <summary>
    ///     查询LFS对象变更
    /// </summary>
    public class QueryLFSObjectChange : Command {
        private Models.LFSChange change = new Models.LFSChange();

        public QueryLFSObjectChange(string repo, string args) {
            Cwd = repo;
            Args = $"diff --ignore-cr-at-eol {args}";
        }

        public Models.LFSChange Result() {
            Exec();
            return change;
        }

        public override void OnReadline(string line) {
            var ch = line[0];
            if (ch == '-') {
                if (change.Old == null) change.Old = new Models.LFSObject();
                line = line.Substring(1);
                if (line.StartsWith("oid sha256:")) {
                    change.Old.OID = line.Substring(11);
                } else if (line.StartsWith("size ")) {
                    change.Old.Size = int.Parse(line.Substring(5));
                }
            } else if (ch == '+') {
                if (change.New == null) change.New = new Models.LFSObject();
                line = line.Substring(1);
                if (line.StartsWith("oid sha256:")) {
                    change.New.OID = line.Substring(11);
                } else if (line.StartsWith("size ")) {
                    change.New.Size = int.Parse(line.Substring(5));
                }
            } else if (line.StartsWith(" size ")) {
                change.New.Size = change.Old.Size = int.Parse(line.Substring(6));
            }
        }
    }
}
