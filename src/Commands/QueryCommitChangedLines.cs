using System.Text.RegularExpressions;

namespace SourceGit.Commands
{
    public class QueryCommitChangedLines : Command
    {
        public QueryCommitChangedLines(string repo, string sha)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"show --numstat --oneline {sha}";
        }

        public (int, int) Result()
        {
            _addedLines = 0;
            _removedLines = 0;
            _firstLine = true;
            Exec();
            return (_addedLines, _removedLines);
        }

        protected override void OnReadline(string line)
        {
            if (_firstLine) {
                _firstLine = false;
                return;
            }

            var parts = Regex.Split(line, @"\s+");

            if (parts.Length >= 2)
            {
                bool canParseAdded = int.TryParse(parts[0], out int addedLines);
                bool canParseRemoved = int.TryParse(parts[1], out int removedLines);
                if (canParseAdded) _addedLines += addedLines;
                if (canParseRemoved) _removedLines += removedLines;
            }
        }

        private int _addedLines;
        private int _removedLines;
        private bool _firstLine;
    }
}
