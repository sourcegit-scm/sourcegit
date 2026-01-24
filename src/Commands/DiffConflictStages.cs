using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    /// <summary>
    /// Computes a diff between the conflict stages :2: (ours) and :3: (theirs)
    /// </summary>
    public partial class DiffConflictStages : Command
    {
        [GeneratedRegex(@"^@@ \-(\d+),?\d* \+(\d+),?\d* @@")]
        private static partial Regex REG_INDICATOR();

        public DiffConflictStages(string repo, string file, int unified = 9999)
        {
            WorkingDirectory = repo;
            Context = repo;
            _result.TextDiff = new Models.TextDiff();

            // Diff between stage :2: (ours) and :3: (theirs)
            var builder = new StringBuilder();
            builder.Append("diff --no-color --no-ext-diff --patch ");
            if (Models.DiffOption.IgnoreCRAtEOL)
                builder.Append("--ignore-cr-at-eol ");
            builder.Append("--unified=").Append(unified).Append(' ');
            builder.Append($":2:{file.Quoted()} :3:{file.Quoted()}");

            Args = builder.ToString();
        }

        public async Task<Models.DiffResult> ReadAsync()
        {
            try
            {
                using var proc = new Process();
                proc.StartInfo = CreateGitStartInfo(true);
                proc.Start();

                var text = await proc.StandardOutput.ReadToEndAsync().ConfigureAwait(false);

                var start = 0;
                var end = text.IndexOf('\n', start);
                while (end > 0)
                {
                    var line = text[start..end];
                    ParseLine(line);

                    start = end + 1;
                    end = text.IndexOf('\n', start);
                }

                if (start < text.Length)
                    ParseLine(text[start..]);

                await proc.WaitForExitAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore exceptions.
            }

            if (_result.IsBinary || _result.TextDiff.Lines.Count == 0)
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

            if (_result.TextDiff.Lines.Count == 0)
            {
                if (line.StartsWith("Binary", StringComparison.Ordinal))
                {
                    _result.IsBinary = true;
                    return;
                }

                var match = REG_INDICATOR().Match(line);
                if (!match.Success)
                    return;

                _oldLine = int.Parse(match.Groups[1].Value);
                _newLine = int.Parse(match.Groups[2].Value);
                _last = new Models.TextDiffLine(Models.TextDiffLineType.Indicator, line, 0, 0);
                _result.TextDiff.Lines.Add(_last);
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
                    _last = new Models.TextDiffLine(Models.TextDiffLineType.Deleted, line.Substring(1), _oldLine, 0);
                    _deleted.Add(_last);
                    _oldLine++;
                }
                else if (ch == '+')
                {
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
                                left.Highlights.Add(new Models.TextRange(chunk.DeletedStart, chunk.DeletedCount));

                            if (chunk.AddedCount > 0)
                                right.Highlights.Add(new Models.TextRange(chunk.AddedStart, chunk.AddedCount));
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
