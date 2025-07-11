using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public partial class QuerySubmodules : Command
    {
        [GeneratedRegex(@"^([U\-\+ ])([0-9a-f]+)\s(.*?)(\s\(.*\))?$")]
        private static partial Regex REG_FORMAT_STATUS();
        [GeneratedRegex(@"^\s?[\w\?]{1,4}\s+(.+)$")]
        private static partial Regex REG_FORMAT_DIRTY();
        [GeneratedRegex(@"^submodule\.(\S*)\.(\w+)=(.*)$")]
        private static partial Regex REG_FORMAT_MODULE_INFO();

        public QuerySubmodules(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = "submodule status";
        }

        public async Task<List<Models.Submodule>> GetResultAsync()
        {
            var submodules = new List<Models.Submodule>();
            var rs = await ReadToEndAsync().ConfigureAwait(false);

            var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            var map = new Dictionary<string, Models.Submodule>();
            var needCheckLocalChanges = false;
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
                            needCheckLocalChanges = true;
                            break;
                    }

                    map.Add(path, module);
                    submodules.Add(module);
                }
            }

            if (submodules.Count > 0)
            {
                Args = "config --file .gitmodules --list";
                rs = await ReadToEndAsync().ConfigureAwait(false);
                if (rs.IsSuccess)
                {
                    var modules = new Dictionary<string, ModuleInfo>();
                    lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

                    foreach (var line in lines)
                    {
                        var match = REG_FORMAT_MODULE_INFO().Match(line);
                        if (match.Success)
                        {
                            var name = match.Groups[1].Value;
                            var key = match.Groups[2].Value;
                            var val = match.Groups[3].Value;

                            if (!modules.TryGetValue(name, out var m))
                            {
                                // Find name alias.
                                foreach (var kv in modules)
                                {
                                    if (kv.Value.Path.Equals(name, StringComparison.Ordinal))
                                    {
                                        m = kv.Value;
                                        break;
                                    }
                                }

                                if (m == null)
                                {
                                    m = new ModuleInfo();
                                    modules.Add(name, m);
                                }
                            }

                            if (key.Equals("path", StringComparison.Ordinal))
                                m.Path = val;
                            else if (key.Equals("url", StringComparison.Ordinal))
                                m.URL = val;
                            else if (key.Equals("branch", StringComparison.Ordinal))
                                m.Branch = val;
                        }
                    }

                    foreach (var kv in modules)
                    {
                        if (map.TryGetValue(kv.Value.Path, out var m))
                        {
                            m.URL = kv.Value.URL;
                            m.Branch = kv.Value.Branch;
                        }
                    }
                }
            }

            if (needCheckLocalChanges)
            {
                var builder = new StringBuilder();
                foreach (var kv in map)
                {
                    if (kv.Value.Status == Models.SubmoduleStatus.Normal)
                        builder.Append(kv.Key.Quoted()).Append(' ');
                }

                Args = $"--no-optional-locks status --porcelain -- {builder}";
                rs = await ReadToEndAsync().ConfigureAwait(false);
                if (!rs.IsSuccess)
                    return submodules;

                lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var match = REG_FORMAT_DIRTY().Match(line);
                    if (match.Success)
                    {
                        var path = match.Groups[1].Value;
                        if (map.TryGetValue(path, out var m))
                            m.Status = Models.SubmoduleStatus.Modified;
                    }
                }
            }

            return submodules;
        }

        private class ModuleInfo
        {
            public string Path { get; set; } = string.Empty;
            public string URL { get; set; } = string.Empty;
            public string Branch { get; set; } = "HEAD";
        }
    }
}
