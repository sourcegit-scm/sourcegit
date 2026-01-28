using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
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

            var builder = new StringBuilder(1024);
            builder.Append("ls-tree ").Append(sha);
            if (!string.IsNullOrEmpty(parentFolder))
                builder.Append(" -- ").Append(parentFolder.Quoted());

            Args = builder.ToString();
        }

        public async Task<List<Models.Object>> GetResultAsync()
        {
            var outs = new List<Models.Object>();

            try
            {
                using var proc = new Process();
                proc.StartInfo = CreateGitStartInfo(true);
                proc.Start();

                while (await proc.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { } line)
                {
                    var match = REG_FORMAT().Match(line);
                    if (!match.Success)
                        continue;

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

                await proc.WaitForExitAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore exceptions.
            }

            return outs;
        }
    }
}
