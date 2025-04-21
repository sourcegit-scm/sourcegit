using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SourceGit.Commands
{
    public partial class QueryAssumeUnchangedFiles : Command
    {
        [GeneratedRegex(@"^(\w)\s+(.+)$")]
        private static partial Regex REG_PARSE();

        public QueryAssumeUnchangedFiles(string repo)
        {
            WorkingDirectory = repo;
            Args = "ls-files -v";
            RaiseError = false;
        }

        public List<string> Result()
        {
            var outs = new List<string>();
            var rs = ReadToEnd();
            var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var match = REG_PARSE().Match(line);
                if (!match.Success)
                    continue;

                if (match.Groups[1].Value == "h")
                    outs.Add(match.Groups[2].Value);
            }

            return outs;
        }
    }
}
