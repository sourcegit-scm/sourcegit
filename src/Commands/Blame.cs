using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public partial class Blame : Command
    {
        [GeneratedRegex(@"^\^?([0-9a-f]+)\s+.*\((.*)\s+(\d+)\s+[\-\+]?\d+\s+\d+\) (.*)")]
        private static partial Regex REG_FORMAT();

        public Blame(string repo, string file, string revision)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"blame -t {revision} -- {file.Quoted()}";
            RaiseError = false;

            _result.File = file;
        }

        public async Task<Models.BlameData> ReadAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return _result;

            var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                ParseLine(line);

                if (_result.IsBinary)
                    break;
            }

            if (_needUnifyCommitSHA)
            {
                foreach (var line in _result.LineInfos)
                {
                    if (line.CommitSHA.Length > _minSHALen)
                        line.CommitSHA = line.CommitSHA.Substring(0, _minSHALen);
                }
            }

            _result.Content = _content.ToString();
            return _result;
        }

        private void ParseLine(string line)
        {
            if (line.Contains('\0'))
            {
                _result.IsBinary = true;
                _result.LineInfos.Clear();
                return;
            }

            var match = REG_FORMAT().Match(line);
            if (!match.Success)
                return;

            _content.AppendLine(match.Groups[4].Value);

            var commit = match.Groups[1].Value;
            var author = match.Groups[2].Value;
            var timestamp = int.Parse(match.Groups[3].Value);
            var when = DateTime.UnixEpoch.AddSeconds(timestamp).ToLocalTime().ToString(_dateFormat);

            var info = new Models.BlameLineInfo()
            {
                IsFirstInGroup = commit != _lastSHA,
                CommitSHA = commit,
                Author = author,
                Time = when,
            };

            _result.LineInfos.Add(info);
            _lastSHA = commit;

            if (line[0] == '^')
            {
                _needUnifyCommitSHA = true;
                _minSHALen = Math.Min(_minSHALen, commit.Length);
            }
        }

        private readonly Models.BlameData _result = new Models.BlameData();
        private readonly StringBuilder _content = new StringBuilder();
        private readonly string _dateFormat = Models.DateTimeFormat.Active.DateOnly;
        private string _lastSHA = string.Empty;
        private bool _needUnifyCommitSHA = false;
        private int _minSHALen = 64;
    }
}
