using System.Text.RegularExpressions;

namespace SourceGit.Commands
{
    public class QueryCommitChangedLines : Command
    {
        public QueryCommitChangedLines(string repo, string sha)
        {
            WorkingDirectory = repo;
            Context = repo;
            // Use shortstat for faster results, which is enough for our needs
            Args = $"show --shortstat --oneline {sha}";
            _pattern = new Regex(@"(\d+) files? changed(?:, (\d+) insertions?\(\+\))?(?:, (\d+) deletions?\(-\))?");
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
            var match = _pattern.Match(line);
            if (match.Success)
            {
                if (match.Groups[2].Success)
                {
                    _addedLines = int.Parse(match.Groups[2].Value);
                }
                
                if (match.Groups[3].Success)
                {
                    _removedLines = int.Parse(match.Groups[3].Value);
                }
            }
        }

        private readonly Regex _pattern;
        private int _addedLines;
        private int _removedLines;
    }
}
