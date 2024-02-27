using System.Text.RegularExpressions;

namespace SourceGit.Commands {
    public class QueryStagedFileBlobGuid : Command {
        private static readonly Regex REG_FORMAT = new Regex(@"^\d+\s+([0-9a-f]+)\s+.*$");

        public QueryStagedFileBlobGuid(string repo, string file) {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"ls-files -s -- \"{file}\"";
        }

        public string Result() {
            var rs = ReadToEnd();
            var match = REG_FORMAT.Match(rs.StdOut.Trim());
            if (match.Success) {
                return match.Groups[1].Value;
            }

            return string.Empty;
        }
    }
}
