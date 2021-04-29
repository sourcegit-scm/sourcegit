using System.Collections.Generic;

namespace SourceGit.Commands {
    /// <summary>
    ///     取得指定提交下的某文件内容
    /// </summary>
    public class QueryFileContent : Command {
        private List<Models.TextLine> lines = new List<Models.TextLine>();
        private int added = 0;

        public QueryFileContent(string repo, string commit, string path) {
            Cwd = repo;
            Args = $"show {commit}:\"{path}\"";
        }

        public List<Models.TextLine> Result() {
            Exec();
            return lines;
        }

        public override void OnReadline(string line) {
            added++;
            lines.Add(new Models.TextLine() { Number = added, Data = line });
        }
    }
}
