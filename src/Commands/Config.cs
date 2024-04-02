using System;
using System.Collections.Generic;

namespace SourceGit.Commands
{
    public class Config : Command
    {
        public Config(string repository)
        {
            WorkingDirectory = repository;
            Context = repository;
            RaiseError = false;
        }

        public Dictionary<string, string> ListAll()
        {
            Args = "config -l";

            var output = ReadToEnd();
            var rs = new Dictionary<string, string>();
            if (output.IsSuccess)
            {
                var lines = output.StdOut.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var idx = line.IndexOf('=', StringComparison.Ordinal);
                    if (idx != -1)
                    {
                        var key = line.Substring(0, idx).Trim();
                        var val = line.Substring(idx + 1).Trim();
                        if (rs.ContainsKey(key))
                        {
                            rs[key] = val;
                        }
                        else
                        {
                            rs.Add(key, val);
                        }
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
            if (!allowEmpty && string.IsNullOrWhiteSpace(value))
            {
                if (string.IsNullOrEmpty(WorkingDirectory))
                {
                    Args = $"config --global --unset {key}";
                }
                else
                {
                    Args = $"config --unset {key}";
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(WorkingDirectory))
                {
                    Args = $"config --global {key} \"{value}\"";
                }
                else
                {
                    Args = $"config {key} \"{value}\"";
                }
            }

            return Exec();
        }
    }
}
