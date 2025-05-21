using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SourceGit.Commands
{
    public partial class QueryUpdatableSubmodules : Command
    {
        [GeneratedRegex(@"^([U\-\+ ])([0-9a-f]+)\s(.*?)(\s\(.*\))?$")]
        private static partial Regex REG_FORMAT_STATUS();

        public QueryUpdatableSubmodules(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = "submodule status";
        }

        public List<string> Result()
        {
            var submodules = new List<string>();
            var rs = ReadToEnd();

            var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var match = REG_FORMAT_STATUS().Match(line);
                if (match.Success)
                {
                    var stat = match.Groups[1].Value;
                    var path = match.Groups[3].Value;
                    if (!stat.StartsWith(' '))
                        submodules.Add(path);
                }
            }

            return submodules;
        }
    }
}
