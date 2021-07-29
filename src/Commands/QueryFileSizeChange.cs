using System.IO;

namespace SourceGit.Commands {
    /// <summary>
    ///     查询文件大小变化
    /// </summary>
    public class QueryFileSizeChange {

        class QuerySizeCmd : Command {
            public QuerySizeCmd(string repo, string path, string revision) {
                Cwd = repo;
                Args = $"cat-file -s {revision}:\"{path}\"";
            }

            public long Result() {
                string data = ReadToEnd().Output;
                long size;
                if (!long.TryParse(data, out size)) size = 0;
                return size;
            }
        }

        private Models.FileSizeChange change = new Models.FileSizeChange();

        public QueryFileSizeChange(string repo, string[] revisions, string path, string orgPath) {
            if (revisions.Length == 0) {
                change.NewSize = new FileInfo(Path.Combine(repo, path)).Length;
                change.OldSize = new QuerySizeCmd(repo, path, "HEAD").Result();
            } else if (revisions.Length == 1) {
                change.NewSize = new QuerySizeCmd(repo, path, "HEAD").Result();
                if (string.IsNullOrEmpty(orgPath)) {
                    change.OldSize = new QuerySizeCmd(repo, path, revisions[0]).Result();
                } else {
                    change.OldSize = new QuerySizeCmd(repo, orgPath, revisions[0]).Result();
                }
            } else {
                change.NewSize = new QuerySizeCmd(repo, path, revisions[1]).Result();
                if (string.IsNullOrEmpty(orgPath)) {
                    change.OldSize = new QuerySizeCmd(repo, path, revisions[0]).Result();
                } else {
                    change.OldSize = new QuerySizeCmd(repo, orgPath, revisions[0]).Result();
                }
            }
        }

        public Models.FileSizeChange Result() {
            return change;
        }
    }
}
