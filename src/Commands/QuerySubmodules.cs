using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SourceGit.Commands
{
    public partial class QuerySubmodules : Command
    {
        [GeneratedRegex(@"^([U\-\+ ])([0-9a-f]+)\s(.*?)(\s\(.*\))?$")]
        private static partial Regex REG_FORMAT_STATUS();
        [GeneratedRegex(@"^\s?[\w\?]{1,4}\s+(.+)$")]
        private static partial Regex REG_FORMAT_DIRTY();

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

            var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            var needCheckLocalChanges = new Dictionary<string, Models.Submodule>();
            foreach (var line in lines)
            {
                var match = REG_FORMAT_STATUS().Match(line);
                if (match.Success)
                {
                    var stat = match.Groups[1].Value;
                    var sha = match.Groups[2].Value;
                    var path = match.Groups[3].Value;

                    var module = new Models.Submodule() { Path = path, SHA = sha };
                    switch (stat[0])
                    {
                        case '-':
                            module.Status = Models.SubmoduleStatus.NotInited;
                            break;
                        case '+':
                            module.Status = Models.SubmoduleStatus.RevisionChanged;
                            break;
                        case 'U':
                            module.Status = Models.SubmoduleStatus.Unmerged;
                            break;
                        default:
                            module.Status = Models.SubmoduleStatus.Normal;
                            needCheckLocalChanges.Add(path, module);
                            break;
                    }

                    submodules.Add(module);
                }
            }

            if (needCheckLocalChanges.Count > 0)
            {
                var builder = new StringBuilder();
                foreach (var kv in needCheckLocalChanges)
                {
                    builder.Append('"');
                    builder.Append(kv.Key);
                    builder.Append("\" ");
                }

                Args = $"--no-optional-locks status -uno --porcelain -- {builder}";
                rs = ReadToEnd();
                if (!rs.IsSuccess)
                    return submodules;

                lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var match = REG_FORMAT_DIRTY().Match(line);
                    if (match.Success)
                    {
                        var path = match.Groups[1].Value;
                        if (needCheckLocalChanges.TryGetValue(path, out var m))
                            m.Status = Models.SubmoduleStatus.Modified;
                    }
                }
            }

            return submodules;
        }
    }
}
