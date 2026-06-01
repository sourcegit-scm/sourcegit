using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public partial class Diff : Command
    {
        [GeneratedRegex(@"^@@ \-(\d+),?\d* \+(\d+),?\d* @@")]
        private static partial Regex REG_INDICATOR();

        [GeneratedRegex(@"^index\s([0-9a-f]{6,64})\.\.([0-9a-f]{6,64})(\s[1-9]{6})?")]
        private static partial Regex REG_HASH_CHANGE();

        private const string PREFIX_LFS_NEW = "+version https://git-lfs.github.com/spec/";
        private const string PREFIX_LFS_DEL = "-version https://git-lfs.github.com/spec/";
        private const string PREFIX_LFS_MODIFY = " version https://git-lfs.github.com/spec/";

        public Diff(string repo, Models.DiffOption opt, int unified, bool ignoreWhitespace, bool ignoreCase = false)
        {
            _result.TextDiff = new Models.TextDiff();
            _ignoreCase = ignoreCase;

            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder(256);
            builder.Append("diff --no-color --no-ext-diff --patch ");
            if (Models.DiffOption.IgnoreCRAtEOL)
                builder.Append("--ignore-cr-at-eol ");
            if (ignoreWhitespace)
                builder.Append("--ignore-all-space --ignore-blank-lines ");
            builder.Append("--unified=").Append(unified).Append(' ');
            builder.Append(opt.ToString());

            Args = builder.ToString();
        }

        public async Task<Models.DiffResult> ReadAsync()
        {
            try
            {
                using var proc = new Process();
                proc.StartInfo = CreateGitStartInfo(true);
                proc.Start();

                using var ms = new MemoryStream();
                await proc.StandardOutput.BaseStream.CopyToAsync(ms, CancellationToken).ConfigureAwait(false);

                var bytes = ms.ToArray();
                var start = 0;
                while (start < bytes.Length)
                {
                    var end = Array.IndexOf(bytes, (byte)'\n', start);
                    if (end < 0)
                    {
                        ParseLine(bytes[start..]);
                        break;
                    }

                    ParseLine(bytes[start..end]);
                    if (_result.IsBinary)
                        break;

                    start = end + 1;
                }

                await proc.WaitForExitAsync(CancellationToken).ConfigureAwait(false);
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

        private void ParseLine(byte[] lineBytes)
        {
            var line = Encoding.UTF8.GetString(lineBytes);
            if (ParseFileModeChange(line))
                return;

            if (ParseLFSChange(line))
                return;

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
                    _last = new Models.TextDiffLine(Models.TextDiffLineType.Indicator, line, lineBytes, 0, 0);
                    _result.TextDiff.Lines.Add(_last);
                }
            }
            else
            {
                if (line.Length == 0)
                {
                    ProcessInlineHighlights();
                    _last = new Models.TextDiffLine(Models.TextDiffLineType.Normal, "", [], _oldLine, _newLine);
                    _result.TextDiff.Lines.Add(_last);
                    _oldLine++;
                    _newLine++;
                    return;
                }

                var ch = line[0];
                if (ch == '-')
                {
                    _result.TextDiff.DeletedLines++;
                    _last = new Models.TextDiffLine(Models.TextDiffLineType.Deleted, line.Substring(1), lineBytes[1..], _oldLine, 0);
                    _deleted.Add(_last);
                    _oldLine++;
                }
                else if (ch == '+')
                {
                    _result.TextDiff.AddedLines++;
                    _last = new Models.TextDiffLine(Models.TextDiffLineType.Added, line.Substring(1), lineBytes[1..], 0, _newLine);
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
                        _last = new Models.TextDiffLine(Models.TextDiffLineType.Indicator, line, lineBytes, 0, 0);
                        _result.TextDiff.Lines.Add(_last);
                    }
                    else
                    {
                        _last = new Models.TextDiffLine(Models.TextDiffLineType.Normal, line.Substring(1), lineBytes[1..], _oldLine, _newLine);
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

        private bool ParseFileModeChange(string line)
        {
            if (line.StartsWith("old mode ", StringComparison.Ordinal))
            {
                _result.OldMode = line.Substring(9);
                return true;
            }

            if (line.StartsWith("new mode ", StringComparison.Ordinal))
            {
                _result.NewMode = line.Substring(9);
                return true;
            }

            if (line.StartsWith("deleted file mode ", StringComparison.Ordinal))
            {
                _result.OldMode = line.Substring(18);
                return true;
            }

            if (line.StartsWith("new file mode ", StringComparison.Ordinal))
            {
                _result.NewMode = line.Substring(14);
                return true;
            }

            return false;
        }

        private bool ParseLFSChange(string line)
        {
            if (_result.IsLFS)
            {
                if (line.StartsWith("-oid sha256:", StringComparison.Ordinal))
                    _result.LFSDiff.Old.Oid = line.Substring(12);
                else if (line.StartsWith("-size ", StringComparison.Ordinal))
                    _result.LFSDiff.Old.Size = long.Parse(line.AsSpan(6));
                else if (line.StartsWith("+oid sha256:", StringComparison.Ordinal))
                    _result.LFSDiff.New.Oid = line.Substring(12);
                else if (line.StartsWith("+size ", StringComparison.Ordinal))
                    _result.LFSDiff.New.Size = long.Parse(line.AsSpan(6));
                else if (line.StartsWith(" size ", StringComparison.Ordinal))
                    _result.LFSDiff.New.Size = _result.LFSDiff.Old.Size = long.Parse(line.AsSpan(6));

                return true;
            }

            if (_result.TextDiff.Lines.Count != 1)
                return false;

            var isLFS = (_oldLine == 1 && _newLine == 1 && line.StartsWith(PREFIX_LFS_MODIFY, StringComparison.Ordinal)) ||
                (_oldLine == 1 && _newLine == 0 && line.StartsWith(PREFIX_LFS_DEL, StringComparison.Ordinal)) ||
                (_oldLine == 0 && _newLine == 1 && line.StartsWith(PREFIX_LFS_NEW, StringComparison.Ordinal));

            if (isLFS)
            {
                _result.IsLFS = true;
                _result.LFSDiff = new Models.LFSDiff();
                return true;
            }

            return false;
        }

        private void ProcessInlineHighlights()
        {
            // Git has no `--ignore-case` option for `diff`, so case-insensitivity is
            // handled here. Within a balanced block (equal number of removed/added
            // lines, which is how a re-casing shows up) any pair that differs only by
            // letter casing is turned into a context (unchanged) line; the remaining
            // real changes keep their normal removed/added rendering.
            if (_ignoreCase && _deleted.Count > 0 && _deleted.Count == _added.Count)
            {
                var pendingDeleted = new List<Models.TextDiffLine>();
                var pendingAdded = new List<Models.TextDiffLine>();

                for (int i = 0; i < _deleted.Count; i++)
                {
                    var del = _deleted[i];
                    var add = _added[i];

                    if (del.Content.Equals(add.Content, StringComparison.OrdinalIgnoreCase))
                    {
                        // Emit any preceding real change first so line order is preserved.
                        FlushBlock(pendingDeleted, pendingAdded);

                        _result.TextDiff.DeletedLines--;
                        _result.TextDiff.AddedLines--;
                        _result.TextDiff.Lines.Add(new Models.TextDiffLine(
                            Models.TextDiffLineType.Normal,
                            add.Content,
                            add.RawContent,
                            del.OldLineNumber,
                            add.NewLineNumber));
                    }
                    else
                    {
                        pendingDeleted.Add(del);
                        pendingAdded.Add(add);
                    }
                }

                FlushBlock(pendingDeleted, pendingAdded);
                _deleted.Clear();
                _added.Clear();
                return;
            }

            FlushBlock(_deleted, _added);
        }

        private void FlushBlock(List<Models.TextDiffLine> deleted, List<Models.TextDiffLine> added)
        {
            if (deleted.Count > 0)
            {
                if (added.Count == deleted.Count)
                {
                    for (int i = added.Count - 1; i >= 0; i--)
                    {
                        var left = deleted[i];
                        var right = added[i];

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

                _result.TextDiff.Lines.AddRange(deleted);
                deleted.Clear();
            }

            if (added.Count > 0)
            {
                _result.TextDiff.Lines.AddRange(added);
                added.Clear();
            }
        }

        private readonly Models.DiffResult _result = new Models.DiffResult();
        private readonly List<Models.TextDiffLine> _deleted = new List<Models.TextDiffLine>();
        private readonly List<Models.TextDiffLine> _added = new List<Models.TextDiffLine>();
        private Models.TextDiffLine _last = null;
        private int _oldLine = 0;
        private int _newLine = 0;
        private readonly bool _ignoreCase = false;
    }
}
