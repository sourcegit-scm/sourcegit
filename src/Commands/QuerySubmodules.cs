using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SourceGit.Commands
{
    public partial class QuerySubmodules : Command
    {
        [GeneratedRegex(@"^[U\-\+ ][0-9a-f]+\s(.*)\s\(.*\)$")]
        private static partial Regex REG_FORMAT1();
        [GeneratedRegex(@"^[U\-\+ ][0-9a-f]+\s(.*)$")]
        private static partial Regex REG_FORMAT2();
        [GeneratedRegex(@"^\s?[\w\?]{1,4}\s+(.+)$")]
        private static partial Regex REG_FORMAT_STATUS();

        public QuerySubmodules(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = "submodule status";
        }

        public List<Models.Submodule> Result()
        {
            var submodules = new List<Models.Submodule>();
            var rs = ReadToEnd();

            var builder = new StringBuilder();
            var lines = rs.StdOut.Split(['\r', '\n'], System.StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var match = REG_FORMAT1().Match(line);
                if (match.Success)
                {
                    var path = match.Groups[1].Value;
                    builder.Append($"\"{path}\" ");
                    submodules.Add(new Models.Submodule() { Path = path });
                    continue;
                }

                match = REG_FORMAT2().Match(line);
                if (match.Success)
                {
                    var path = match.Groups[1].Value;
                    builder.Append($"\"{path}\" ");
                    submodules.Add(new Models.Submodule() { Path = path });
                }
            }

            if (submodules.Count > 0)
            {
                Args = $"--no-optional-locks status -uno --porcelain -- {builder}";
                rs = ReadToEnd();
                if (!rs.IsSuccess)
                    return submodules;

                var dirty = new HashSet<string>();
                lines = rs.StdOut.Split(['\r', '\n'], System.StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var match = REG_FORMAT_STATUS().Match(line);
                    if (match.Success)
                    {
                        var path = match.Groups[1].Value;
                        dirty.Add(path);
                    }
                }

                foreach (var submodule in submodules)
                    submodule.IsDirty = dirty.Contains(submodule.Path);
            }

            return submodules;
        }
    }
}
