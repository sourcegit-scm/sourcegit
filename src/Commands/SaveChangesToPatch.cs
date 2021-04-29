using System.IO;

namespace SourceGit.Commands {
    /// <summary>
    ///     将Changes保存到文件流中
    /// </summary>
    public class SaveChangeToStream : Command {
        private StreamWriter writer = null;

        public SaveChangeToStream(string repo, Models.Change change, StreamWriter to) {
            Cwd = repo;
            if (change.WorkTree == Models.Change.Status.Added || change.WorkTree == Models.Change.Status.Untracked) {
                Args = $"diff --no-index --no-ext-diff --find-renames -- /dev/null \"{change.Path}\"";
            } else {
                var pathspec = $"\"{change.Path}\"";
                if (!string.IsNullOrEmpty(change.OriginalPath)) pathspec = $"\"{change.OriginalPath}\" \"{change.Path}\"";
                Args = $"diff --binary --no-ext-diff --find-renames --full-index -- {pathspec}";
            }
            writer = to;
        }

        public override void OnReadline(string line) {
            writer.WriteLine(line);
        }
    }
}
