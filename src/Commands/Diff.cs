using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public partial class Diff : Command
    {
        [GeneratedRegex(@"^@@ \-(\d+),?\d* \+(\d+),?\d* @@")]
        private static partial Regex REG_INDICATOR();

        [GeneratedRegex(@"^index\s([0-9a-f]{6,40})\.\.([0-9a-f]{6,40})(\s[1-9]{6})?")]
        private static partial Regex REG_HASH_CHANGE();

        private const string PREFIX_LFS_NEW = "+version https://git-lfs.github.com/spec/";
        private const string PREFIX_LFS_DEL = "-version https://git-lfs.github.com/spec/";
        private const string PREFIX_LFS_MODIFY = " version https://git-lfs.github.com/spec/";

        public Diff(string repo, Models.DiffOption opt, int unified, bool ignoreWhitespace)
        {
            _result.TextDiff = new Models.TextDiff() { Option = opt };

            WorkingDirectory = repo;
            Context = repo;

            if (ignoreWhitespace)
                Args = $"diff --no-ext-diff --patch --ignore-all-space --unified={unified} {opt}";
            else if (Models.DiffOption.IgnoreCRAtEOL)
                Args = $"diff --no-ext-diff --patch --ignore-cr-at-eol --unified={unified} {opt}";
            else
                Args = $"diff --no-ext-diff --patch --unified={unified} {opt}";
        }

        public async Task<Models.DiffResult> ReadAsync()
        {
            try
            {
                using var proc = new Process();
                proc.StartInfo = CreateGitStartInfo(true);
                proc.Start();

                while (await proc.StandardOutput.ReadLineAsync() is { } line)
                    ParseLine(line);

                await proc.WaitForExitAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore exceptions.
            }

            if (_result.IsBinary || _result.IsLFS || _result.TextDiff.Lines.Count == 0)
            {
                _result.TextDiff = null;
            }
            else
            {
                ProcessInlineHighlights();
                _result.TextDiff.MaxLineNumber = Math.Max(_newLine, _oldLine);
            }

            return _result;
        }

        private void ParseLine(string line)
        {
            if (_result.IsBinary)
                return;

            if (line.StartsWith("old mode ", StringComparison.Ordinal))
            {
                _result.OldMode = line.Substring(9);
                return;
            }

            if (line.StartsWith("new mode ", StringComparison.Ordinal))
            {
                _result.NewMode = line.Substring(9);
                return;
            }

            if (line.StartsWith("deleted file mode ", StringComparison.Ordinal))
            {
                _result.OldMode = line.Substring(18);
                return;
            }

            if (line.StartsWith("new file mode ", StringComparison.Ordinal))
            {
                _result.NewMode = line.Substring(14);
                return;
            }

            if (_result.IsLFS)
            {
                var ch = line[0];
                if (ch == '-')
                {
                    if (line.StartsWith("-oid sha256:", StringComparison.Ordinal))
                    {
                        _result.LFSDiff.Old.Oid = line.Substring(12);
                    }
                    else if (line.StartsWith("-size ", StringComparison.Ordinal))
                    {
                        _result.LFSDiff.Old.Size = long.Parse(line.AsSpan(6));
                    }
                }
                else if (ch == '+')
                {
                    if (line.StartsWith("+oid sha256:", StringComparison.Ordinal))
                    {
                        _result.LFSDiff.New.Oid = line.Substring(12);
                    }
                    else if (line.StartsWith("+size ", StringComparison.Ordinal))
                    {
                        _result.LFSDiff.New.Size = long.Parse(line.AsSpan(6));
                    }
                }
                else if (line.StartsWith(" size ", StringComparison.Ordinal))
                {
                    _result.LFSDiff.New.Size = _result.LFSDiff.Old.Size = long.Parse(line.AsSpan(6));
                }
                return;
            }

            if (_result.TextDiff.Lines.Count == 0)
            {
                if (line.StartsWith("Binary", StringComparison.Ordinal))
                {
                    _result.IsBinary = true;
                    return;
                }

                if (string.IsNullOrEmpty(_result.OldHash))
                {
                    var match = REG_HASH_CHANGE().Match(line);
                    if (!match.Success)
                        return;

                    _result.OldHash = match.Groups[1].Value;
                    _result.NewHash = match.Groups[2].Value;
                }
                else
                {
                    var match = REG_INDICATOR().Match(line);
                    if (!match.Success)
                        return;

                    _oldLine = int.Parse(match.Groups[1].Value);
                    _newLine = int.Parse(match.Groups[2].Value);
                    _last = new Models.TextDiffLine(Models.TextDiffLineType.Indicator, line, 0, 0);
                    _result.TextDiff.Lines.Add(_last);
                }
            }
            else
            {
                if (line.Length == 0)
                {
                    ProcessInlineHighlights();
                    _last = new Models.TextDiffLine(Models.TextDiffLineType.Normal, "", _oldLine, _newLine);
                    _result.TextDiff.Lines.Add(_last);
                    _oldLine++;
                    _newLine++;
                    return;
                }

                var ch = line[0];
                if (ch == '-')
                {
                    if (_oldLine == 1 && _newLine == 0 && line.StartsWith(PREFIX_LFS_DEL, StringComparison.Ordinal))
                    {
                        _result.IsLFS = true;
                        _result.LFSDiff = new Models.LFSDiff();
                        return;
                    }

                    _last = new Models.TextDiffLine(Models.TextDiffLineType.Deleted, line.Substring(1), _oldLine, 0);
                    _deleted.Add(_last);
                    _oldLine++;
                }
                else if (ch == '+')
                {
                    if (_oldLine == 0 && _newLine == 1 && line.StartsWith(PREFIX_LFS_NEW, StringComparison.Ordinal))
                    {
                        _result.IsLFS = true;
                        _result.LFSDiff = new Models.LFSDiff();
                        return;
                    }

                    _last = new Models.TextDiffLine(Models.TextDiffLineType.Added, line.Substring(1), 0, _newLine);
                    _added.Add(_last);
                    _newLine++;
                }
                else if (ch != '\\')
                {
                    ProcessInlineHighlights();
                    var match = REG_INDICATOR().Match(line);
                    if (match.Success)
                    {
                        _oldLine = int.Parse(match.Groups[1].Value);
                        _newLine = int.Parse(match.Groups[2].Value);
                        _last = new Models.TextDiffLine(Models.TextDiffLineType.Indicator, line, 0, 0);
                        _result.TextDiff.Lines.Add(_last);
                    }
                    else
                    {
                        if (_oldLine == 1 && _newLine == 1 && line.StartsWith(PREFIX_LFS_MODIFY, StringComparison.Ordinal))
                        {
                            _result.IsLFS = true;
                            _result.LFSDiff = new Models.LFSDiff();
                            return;
                        }

                        _last = new Models.TextDiffLine(Models.TextDiffLineType.Normal, line.Substring(1), _oldLine, _newLine);
                        _result.TextDiff.Lines.Add(_last);
                        _oldLine++;
                        _newLine++;
                    }
                }
                else if (line.Equals("\\ No newline at end of file", StringComparison.Ordinal))
                {
                    _last.NoNewLineEndOfFile = true;
                }
            }
        }

        private void ProcessInlineHighlights()
        {
            if (_deleted.Count > 0)
            {
                if (_added.Count == _deleted.Count)
                {
                    for (int i = _added.Count - 1; i >= 0; i--)
                    {
                        var left = _deleted[i];
                        var right = _added[i];

                        if (left.Content.Length > 1024 || right.Content.Length > 1024)
                            continue;

                        var chunks = Models.TextInlineChange.Compare(left.Content, right.Content);
                        if (chunks.Count > 4)
                            continue;

                        foreach (var chunk in chunks)
                        {
                            if (chunk.DeletedCount > 0)
                                left.Highlights.Add(new Models.TextInlineRange(chunk.DeletedStart, chunk.DeletedCount));

                            if (chunk.AddedCount > 0)
                                right.Highlights.Add(new Models.TextInlineRange(chunk.AddedStart, chunk.AddedCount));
                        }
                    }
                }

                _result.TextDiff.Lines.AddRange(_deleted);
                _deleted.Clear();
            }

            if (_added.Count > 0)
            {
                _result.TextDiff.Lines.AddRange(_added);
                _added.Clear();
            }
        }

        private readonly Models.DiffResult _result = new Models.DiffResult();
        private readonly List<Models.TextDiffLine> _deleted = new List<Models.TextDiffLine>();
        private readonly List<Models.TextDiffLine> _added = new List<Models.TextDiffLine>();
        private Models.TextDiffLine _last = null;
        private int _oldLine = 0;
        private int _newLine = 0;
    }
}
