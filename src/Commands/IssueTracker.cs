using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class IssueTracker : Command
    {
        public IssueTracker(string repo, string storage)
        {
            WorkingDirectory = repo;
            Context = repo;
            _storage = storage;
        }

        public async Task ReadAllAsync(List<Models.IssueTracker> outs, bool isShared)
        {
            if (!File.Exists(_storage))
                return;

            Args = $"config -f {_storage.Quoted()} -l";

            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (rs.IsSuccess)
            {
                var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
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
                        FindOrAdd(outs, ruleName, isShared).RegexString = value;
                    }
                    else if (key.EndsWith(".url", StringComparison.Ordinal))
                    {
                        var prefixLen = "issuetracker.".Length;
                        var suffixLen = ".url".Length;
                        var ruleName = key.Substring(prefixLen, key.Length - prefixLen - suffixLen);
                        FindOrAdd(outs, ruleName, isShared).URLTemplate = value;
                    }
                }
            }
        }

        public async Task<bool> AddAsync(Models.IssueTracker rule)
        {
            Args = $"config -f {_storage.Quoted()} issuetracker.{rule.Name.Quoted()}.regex {rule.RegexString.Quoted()}";

            var succ = await ExecAsync().ConfigureAwait(false);
            if (succ)
            {
                Args = $"config -f {_storage.Quoted()} issuetracker.{rule.Name.Quoted()}.url {rule.URLTemplate.Quoted()}";
                return await ExecAsync().ConfigureAwait(false);
            }

            return false;
        }

        public async Task<bool> RemoveAsync(Models.IssueTracker rule)
        {
            if (!File.Exists(_storage))
                return true;

            Args = $"config -f {_storage.Quoted()} --remove-section issuetracker.{rule.Name.Quoted()}";
            return await ExecAsync().ConfigureAwait(false);
        }

        private Models.IssueTracker FindOrAdd(List<Models.IssueTracker> rules, string ruleName, bool isShared)
        {
            var rule = rules.Find(x => x.Name.Equals(ruleName, StringComparison.Ordinal));
            if (rule != null)
                return rule;

            rule = new Models.IssueTracker() { IsShared = isShared, Name = ruleName };
            rules.Add(rule);
            return rule;
        }

        private readonly string _storage;
    }
}
