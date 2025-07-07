using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public partial class QueryRevisionObjects : Command
    {
        [GeneratedRegex(@"^\d+\s+(\w+)\s+([0-9a-f]+)\s+(.*)$")]
        private static partial Regex REG_FORMAT();

        public QueryRevisionObjects(string repo, string sha, string parentFolder)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"ls-tree -z {sha}";

            if (!string.IsNullOrEmpty(parentFolder))
                Args += $" -- \"{parentFolder}\"";
        }

        public async Task<List<Models.Object>> GetResultAsync()
        {
            var outs = new List<Models.Object>();
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (rs.IsSuccess)
            {
                var start = 0;
                var end = rs.StdOut.IndexOf('\0', start);
                while (end > 0)
                {
                    var line = rs.StdOut.Substring(start, end - start);
                    Parse(outs, line);
                    start = end + 1;
                    end = rs.StdOut.IndexOf('\0', start);
                }

                if (start < rs.StdOut.Length)
                    Parse(outs, rs.StdOut.Substring(start));
            }

            return outs;
        }

        private void Parse(List<Models.Object> outs, string line)
        {
            var match = REG_FORMAT().Match(line);
            if (!match.Success)
                return;

            var obj = new Models.Object();
            obj.SHA = match.Groups[2].Value;
            obj.Type = Models.ObjectType.Blob;
            obj.Path = match.Groups[3].Value;

            obj.Type = match.Groups[1].Value switch
            {
                "blob" => Models.ObjectType.Blob,
                "tree" => Models.ObjectType.Tree,
                "tag" => Models.ObjectType.Tag,
                "commit" => Models.ObjectType.Commit,
                _ => obj.Type,
            };

            outs.Add(obj);
        }
    }
}
