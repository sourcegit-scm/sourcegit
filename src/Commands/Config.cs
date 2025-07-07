using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class Config : Command
    {
        public Config(string repository)
        {
            if (string.IsNullOrEmpty(repository))
            {
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }
            else
            {
                WorkingDirectory = repository;
                Context = repository;
                _isLocal = true;
            }
        }

        public async Task<Dictionary<string, string>> ReadAllAsync()
        {
            Args = "config -l";

            var output = await ReadToEndAsync().ConfigureAwait(false);
            var rs = new Dictionary<string, string>();
            if (output.IsSuccess)
            {
                var lines = output.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var idx = line.IndexOf('=', StringComparison.Ordinal);
                    if (idx != -1)
                    {
                        var key = line.Substring(0, idx).Trim();
                        var val = line.Substring(idx + 1).Trim();
                        rs[key] = val;
                    }
                }
            }

            return rs;
        }

        public async Task<string> GetAsync(string key)
        {
            Args = $"config {key}";

            var rs = await ReadToEndAsync().ConfigureAwait(false);
            return rs.StdOut.Trim();
        }

        public async Task<bool> SetAsync(string key, string value, bool allowEmpty = false)
        {
            var scope = _isLocal ? "--local" : "--global";

            if (!allowEmpty && string.IsNullOrWhiteSpace(value))
                Args = $"config {scope} --unset {key}";
            else
                Args = $"config {scope} {key} \"{value}\"";

            return await ExecAsync().ConfigureAwait(false);
        }

        private bool _isLocal = false;
    }
}
