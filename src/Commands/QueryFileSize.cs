using System.Text.RegularExpressions;

namespace SourceGit.Commands {
    public class QueryFileSize : Command {
        private static readonly Regex REG_FORMAT = new Regex(@"^\d+\s+\w+\s+[0-9a-f]+\s+(\d+)\s+.*$");

        public QueryFileSize(string repo, string file, string revision) {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"ls-tree {revision} -l -- {file}";
        }

        public long Result() {
            if (_result != 0) return _result;

            var rs = ReadToEnd();
            if (rs.IsSuccess) {
                var match = REG_FORMAT.Match(rs.StdOut);
                if (match.Success) {
                    return long.Parse(match.Groups[1].Value);
                }
            }

            return 0;
        }

        private long _result = 0;
    }
}
