using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class SharedIssueTracker : Command
    {
        public SharedIssueTracker(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            _file = $"{repo}/.issuetracker";
        }

        public async Task<List<Models.IssueTrackerRule>> ReadAllAsync()
        {
            if (!File.Exists(_file))
                return [];

            Args = $"config -f {_file.Quoted()} -l";

            var output = await ReadToEndAsync().ConfigureAwait(false);
            var rs = new List<Models.IssueTrackerRule>();
            if (output.IsSuccess)
            {
                var lines = output.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var parts = line.Split('=', 2);
                    if (parts.Length < 2)
                        continue;

                    var key = parts[0];
                    var value = parts[1];

                    if (!key.StartsWith("issuetracker.", StringComparison.Ordinal))
                        continue;

                    if (key.EndsWith(".regex", StringComparison.Ordinal))
                    {
                        var prefixLen = "issuetracker.".Length;
                        var suffixLen = ".regex".Length;
                        var ruleName = key.Substring(prefixLen, key.Length - prefixLen - suffixLen);
                        FindOrAdd(rs, ruleName).RegexString = value;
                    }
                    else if (key.EndsWith(".url", StringComparison.Ordinal))
                    {
                        var prefixLen = "issuetracker.".Length;
                        var suffixLen = ".url".Length;
                        var ruleName = key.Substring(prefixLen, key.Length - prefixLen - suffixLen);
                        FindOrAdd(rs, ruleName).URLTemplate = value;
                    }
                }
            }

            return rs;
        }

        public async Task<bool> AddAsync(Models.IssueTrackerRule rule)
        {
            Args = $"config -f {_file.Quoted()} issuetracker.{rule.Name.Quoted()}.regex {rule.RegexString.Quoted()}";

            var succ = await ExecAsync().ConfigureAwait(false);
            if (succ)
            {
                Args = $"config -f {_file.Quoted()} issuetracker.{rule.Name.Quoted()}.url {rule.URLTemplate.Quoted()}";
                return await ExecAsync().ConfigureAwait(false);
            }

            return false;
        }

        public async Task<bool> RemoveAsync(Models.IssueTrackerRule rule)
        {
            Args = $"config -f {_file.Quoted()} --remove-section issuetracker.{rule.Name.Quoted()}";
            return await ExecAsync().ConfigureAwait(false);
        }

        private Models.IssueTrackerRule FindOrAdd(List<Models.IssueTrackerRule> rules, string ruleName)
        {
            var rule = rules.Find(x => x.Name.Equals(ruleName, StringComparison.Ordinal));
            if (rule != null)
                return rule;

            rule = new Models.IssueTrackerRule() { IsShared = true, Name = ruleName };
            rules.Add(rule);
            return rule;
        }

        private readonly string _file;
    }
}
