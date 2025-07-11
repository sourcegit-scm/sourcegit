using System.Collections.Generic;
using System.IO;
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
            Args = $"ls-tree {sha}";

            if (!string.IsNullOrEmpty(parentFolder))
                Args += $" -- {parentFolder.Quoted()}";
        }

        public async Task<List<Models.Object>> GetResultAsync()
        {
            var outs = new List<Models.Object>();
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (rs.IsSuccess)
            {
                var sr = new StringReader(rs.StdOut);
                while (sr.ReadLine() is { } line)
                    Parse(outs, line);
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
