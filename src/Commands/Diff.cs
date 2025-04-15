using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

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
        private const int MAX_INLINE_HIGHLIGHT_LENGTH = 10240;

        public Diff(string repo, Models.DiffOption opt, int unified, bool ignoreWhitespace)
        {
            _result.TextDiff = new Models.TextDiff()
            {
                Repo = repo,
                Option = opt,
            };

            WorkingDirectory = repo;
            Context = repo;

            if (ignoreWhitespace)
                Args = $"-c core.autocrlf=false diff --no-ext-diff --patch --ignore-cr-at-eol --ignore-all-space --unified={unified} {opt}";
            else
                Args = $"-c core.autocrlf=false diff --no-ext-diff --patch --unified={unified} {opt}";
        }

        public Models.DiffResult Result()
        {
            var rs = ReadToEnd();
            var start = 0;
            var end = rs.StdOut.IndexOf('\n', start);
            while (end > 0)
            {
                var line = rs.StdOut.Substring(start, end - start);
                ParseLine(line);

                start = end + 1;
                end = rs.StdOut.IndexOf('\n', start);
            }

            if (start < rs.StdOut.Length)
                ParseLine(rs.StdOut.Substring(start));

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
                        _result.LFSDiff.Old.Size = long.Parse(line.Substring(6));
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
                        _result.LFSDiff.New.Size = long.Parse(line.Substring(6));
                    }
                }
                else if (line.StartsWith(" size ", StringComparison.Ordinal))
                {
                    _result.LFSDiff.New.Size = _result.LFSDiff.Old.Size = long.Parse(line.Substring(6));
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
                    _result.TextDiff.Lines.Add(new Models.TextDiffLine(Models.TextDiffLineType.Indicator, line, 0, 0));
                }
            }
            else
            {
                if (line.Length == 0)
                {
                    ProcessInlineHighlights();
                    _result.TextDiff.Lines.Add(new Models.TextDiffLine(Models.TextDiffLineType.Normal, "", _oldLine, _newLine));
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

                    _deleted.Add(new Models.TextDiffLine(Models.TextDiffLineType.Deleted, line.Substring(1), _oldLine, 0));
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

                    _added.Add(new Models.TextDiffLine(Models.TextDiffLineType.Added, line.Substring(1), 0, _newLine));
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
                        _result.TextDiff.Lines.Add(new Models.TextDiffLine(Models.TextDiffLineType.Indicator, line, 0, 0));
                    }
                    else
                    {
                        if (_oldLine == 1 && _newLine == 1 && line.StartsWith(PREFIX_LFS_MODIFY, StringComparison.Ordinal))
                        {
                            _result.IsLFS = true;
                            _result.LFSDiff = new Models.LFSDiff();
                            return;
                        }

                        _result.TextDiff.Lines.Add(new Models.TextDiffLine(Models.TextDiffLineType.Normal, line.Substring(1), _oldLine, _newLine));
                        _oldLine++;
                        _newLine++;
                    }
                }
            }
        }

        private void ProcessInlineHighlights()
        {
            if (_deleted.Count > 0 && _added.Count > 0)
            {
                // Compare changes between multiple lines
                var oldContent = ConcatLineContents(_deleted);
                var newContent = ConcatLineContents(_added);
        
                // Skip inline highlights for large content to improve performance
                if (oldContent.Length <= MAX_INLINE_HIGHLIGHT_LENGTH && newContent.Length <= MAX_INLINE_HIGHLIGHT_LENGTH)
                {
                    var chunks = Models.TextInlineChange.CompareMultiLine(oldContent, newContent, _deleted, _added);
            
                    // Apply highlights to corresponding lines
                    foreach (var chunk in chunks)
                    {
                        ApplyHighlight(_deleted, chunk.DeletedLine, chunk.DeletedStart, chunk.DeletedCount);
                        ApplyHighlight(_added, chunk.AddedLine, chunk.AddedStart, chunk.AddedCount);
                    }
                }
            }

            // Add all processed lines to the result
            _result.TextDiff.Lines.AddRange(_deleted);
            _deleted.Clear();
    
            _result.TextDiff.Lines.AddRange(_added);
            _added.Clear();
        }

        private string ConcatLineContents(List<Models.TextDiffLine> lines)
        {
            if (lines.Count == 0)
                return string.Empty;
            var result = new System.Text.StringBuilder();
            for (var i = 0; i < lines.Count; i++)
            {
                if (i > 0)
                    result.Append('\n');
                result.Append(lines[i].Content);
            }
            return result.ToString();
        }

        private void ApplyHighlight(List<Models.TextDiffLine> lines, int lineIndex, int start, int count)
        {
            if (lineIndex >= 0 && lineIndex < lines.Count && count > 0)
            {
                lines[lineIndex].Highlights.Add(new Models.TextInlineRange(start, count));
            }
        }

        private readonly Models.DiffResult _result = new Models.DiffResult();
        private readonly List<Models.TextDiffLine> _deleted = new List<Models.TextDiffLine>();
        private readonly List<Models.TextDiffLine> _added = new List<Models.TextDiffLine>();
        private int _oldLine = 0;
        private int _newLine = 0;
    }
}
