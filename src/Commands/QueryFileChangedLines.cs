using System.Text.RegularExpressions;

namespace SourceGit.Commands
{
    public class QueryFileChangedLines : Command
    {
        public QueryFileChangedLines(string repo, string revision1, string revision2, string filePath)
        {
            WorkingDirectory = repo;
            Context = repo;
            
            if (string.IsNullOrEmpty(revision1) && string.IsNullOrEmpty(revision2))
            {
                // Working copy changes (unstaged)
                Args = $"diff --numstat -- \"{filePath}\"";
            }
            else if (string.IsNullOrEmpty(revision1) && revision2 == "--staged")
            {
                // Staged changes
                Args = $"diff --cached --numstat -- \"{filePath}\"";
            }
            else
            {
                // Comparing two revisions
                Args = $"diff --numstat {revision1} {revision2} -- \"{filePath}\"";
            }
        }

        public (int, int) Result()
        {
            _addedLines = 0;
            _removedLines = 0;
            Exec();
            return (_addedLines, _removedLines);
        }

        protected override void OnReadline(string line)
        {
            var parts = line.Split('\t');
            if (parts.Length >= 2)
            {
                if (int.TryParse(parts[0], out int added))
                {
                    _addedLines = added;
                }

                if (int.TryParse(parts[1], out int removed))
                {
                    _removedLines = removed;
                }
            }
        }

        private int _addedLines;
        private int _removedLines;
    }
}
