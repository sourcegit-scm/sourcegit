using System;
using System.Collections.Generic;

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

            RaiseError = false;
        }

        public Dictionary<string, string> ListAll()
        {
            Args = "config -l";

            var output = ReadToEnd();
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

        public string Get(string key)
        {
            Args = $"config {key}";
            return ReadToEnd().StdOut.Trim();
        }

        public bool Set(string key, string value, bool allowEmpty = false)
        {
            var scope = _isLocal ? "--local" : "--global";

            if (!allowEmpty && string.IsNullOrWhiteSpace(value))
                Args = $"config {scope} --unset {key}";
            else
                Args = $"config {scope} {key} \"{value}\"";

            return Exec();
        }

        private bool _isLocal = false;
    }
}
