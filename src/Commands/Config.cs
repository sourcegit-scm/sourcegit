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

        public Dictionary<string, string> ReadAll()
        {
            Args = "config -l";

            var output = ReadToEnd();
            var rs = new Dictionary<string, string>();
            if (output.IsSuccess)
            {
                var lines = output.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var parts = line.Split('=', 2);
                    if (parts.Length == 2)
                        rs[parts[0]] = parts[1];
                }
            }

            return rs;
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
                    var parts = line.Split('=', 2);
                    if (parts.Length == 2)
                        rs[parts[0]] = parts[1];
                }
            }

            return rs;
        }

        public string Get(string key)
        {
            Args = $"config {key}";
            return ReadToEnd().StdOut.Trim();
        }

        public async Task<string> GetAsync(string key)
        {
            Args = $"config {key}";

            var rs = await ReadToEndAsync().ConfigureAwait(false);
            return rs.StdOut.Trim();
        }

        // Get config value with type canonicalization
        // Weird values will be converted by git, like "000" -> "false", "010" -> "true"
        // git will report bad values like "fatal: bad boolean config value 'kkk' for 'core.untrackedcache'"
        public async Task<bool?> GetBoolAsync(string key)
        {
            Args = $"config get --bool {key}";

            var rs = await ReadToEndAsync().ConfigureAwait(false);
            var stdout = rs.StdOut.Trim();
            switch (rs.StdOut.Trim())
            {
                case "true":
                    return true;
                case "false":
                    return false;
                default:
                    // Illegal values behave as if they are not set
                    return null;
            }
        }

        public async Task<bool> SetAsync(string key, string value, bool allowEmpty = false)
        {
            var scope = _isLocal ? "--local" : "--global";

            if (!allowEmpty && string.IsNullOrWhiteSpace(value))
                Args = $"config {scope} --unset {key}";
            else
                Args = $"config {scope} {key} {value.Quoted()}";

            return await ExecAsync().ConfigureAwait(false);
        }

        private bool _isLocal = false;
    }
}
