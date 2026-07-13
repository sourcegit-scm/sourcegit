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

        private const char PREFIX_CONTEXT = ' ';
        private const char PREFIX_DELETED = '-';
        private const char PREFIX_ADDED = '+';
        private const char PREFIX_COMMAND = '\\';

        private const string FILE_MODE_OLD = "old mode ";
        private const string FILE_MODE_NEW = "new mode ";
        private const string FILE_MODE_DELETED = "deleted file mode ";
        private const string FILE_MODE_ADDED = "new file mode ";

        private const string LFS_SPECIFIER = "version https://git-lfs.github.com/spec/";
        private const string LFS_OID_PREFIX = "oid sha256:";
        private const string LFS_SIZE_PREFIX = "size ";

        private const string SPECIAL_DIFF_START = "diff ";
        private const string SPECIAL_BINARY = "Binary files ";
        private const string SPECIAL_NO_NEWLINE = " No newline at end of file";
        private const string SPECIAL_SUBMODULE = "Subproject commit ";

        public Diff(string repo, Models.DiffOption opt, int numContextLines, bool ignoreWhitespace, bool ignoreCRAtEOL)
        {
            _result.TextDiff = new Models.TextDiff();

            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder(256);
            builder.Append("diff --no-color --no-ext-diff --full-index --patch ");
            if (ignoreWhitespace)
                builder.Append("--ignore-space-change --ignore-blank-lines ");
            if (ignoreCRAtEOL)
                builder.Append("--ignore-cr-at-eol ");
            builder.Append("--unified=").Append(numContextLines).Append(' ');
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

                if (ms.TryGetBuffer(out var buffer))
                {
                    var start = buffer.Offset;
                    var end = buffer.Offset + buffer.Count;
                    while (start < end)
                    {
                        var lineEnd = Array.IndexOf(buffer.Array, (byte)'\n', start);
                        if (lineEnd < 0)
                        {
                            ParseLine(buffer[start..]);
                            break;
                        }

                        ParseLine(buffer[start..lineEnd]);
                        if (_result.IsBinary)
                            break;

                        start = lineEnd + 1;
                    }
                }

                await proc.WaitForExitAsync(CancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Ignore exceptions.
            }

            if (_isLFS || _result.IsBinary || _result.TextDiff.Lines.Count == 0)
            {
                _result.TextDiff = null;
            }
            else
            {
                if (_isInChunk)
                {
                    ProcessInlineHighlights();
                    _isInChunk = false;
                }

                if (_result.TextDiff.Lines.Count < 4)
                {
                    var isSubmoduleChange = true;

                    for (int i = 1; i < _result.TextDiff.Lines.Count; i++)
                    {
                        var line = _result.TextDiff.Lines[i];
                        if (!line.Content.StartsWith(SPECIAL_SUBMODULE, StringComparison.Ordinal))
                        {
                            isSubmoduleChange = false;
                            break;
                        }
                    }

                    if (isSubmoduleChange)
                    {
                        _result.IsSubmoduleChange = true;
                        _result.TextDiff = null;
                        return _result;
                    }
                }

                _result.TextDiff.MaxLineNumber = Math.Max(_newLine, _oldLine);
                _result.TextDiff.OldMode = _result.OldMode;
                _result.TextDiff.NewMode = _result.NewMode;
                _result.TextDiff.OldHash = _result.OldHash;
                _result.TextDiff.NewHash = _result.NewHash;
            }

            return _result;
        }

        private void ParseLine(ArraySegment<byte> lineBytes)
        {
            // Decode line bytes to UTF-8 string
            var line = Encoding.UTF8.GetString(lineBytes.Array, lineBytes.Offset, lineBytes.Count);
            if (line.Length == 0)
                return;

            // If we are reading a chunk body, try to read the current line as body first (because
            // the number of chunk body is greater than the number of chunk indicator in most time.
            if (_isInChunk)
            {
                if (ParseChunkBodyLine(line, lineBytes[1..]))
                    return;

                ProcessInlineHighlights();
                _isInChunk = false;
            }

            // If the current line is not a chunk body, try to parse it as chunk indicator
            if (ParseChunkStartLine(line))
                return;

            // Fallback to diff headers to support type-changed diff (multiple headers).
            ParseDiffHeaderLine(line);
        }

        private void ParseDiffHeaderLine(string line)
        {
            if (line.StartsWith(SPECIAL_DIFF_START, StringComparison.Ordinal))
                return;

            if (ParseFileModeChange(line))
                return;

            var match = REG_HASH_CHANGE().Match(line);
            if (match.Success)
            {
                if (string.IsNullOrEmpty(_result.OldHash))
                    _result.OldHash = match.Groups[1].Value;
                _result.NewHash = match.Groups[2].Value;
                return;
            }

            if (line.StartsWith(SPECIAL_BINARY, StringComparison.Ordinal))
                _result.IsBinary = true;
        }

        private bool ParseChunkStartLine(string line)
        {
            var match = REG_INDICATOR().Match(line);
            if (match.Success)
            {
                _oldLine = int.Parse(match.Groups[1].Value);
                _newLine = int.Parse(match.Groups[2].Value);
                _last = new Models.TextDiffLine(Models.TextDiffLineType.Indicator, line, null, 0, 0);
                _result.TextDiff.Lines.Add(_last);
                _isInChunk = true;
                return true;
            }

            return false;
        }

        private bool ParseChunkBodyLine(string line, ArraySegment<byte> lineBytes)
        {
            var prefix = line[0];
            var content = line.Substring(1);
            if (ParseLFSChange(prefix, content))
                return true;

            if (prefix == PREFIX_DELETED)
            {
                _result.TextDiff.DeletedLines++;
                _last = new Models.TextDiffLine(Models.TextDiffLineType.Deleted, content, lineBytes.ToArray(), _oldLine, 0);
                _deleted.Add(_last);
                _oldLine++;
                return true;
            }

            if (prefix == PREFIX_ADDED)
            {
                _result.TextDiff.AddedLines++;
                _last = new Models.TextDiffLine(Models.TextDiffLineType.Added, content, lineBytes.ToArray(), 0, _newLine);
                _added.Add(_last);
                _newLine++;
                return true;
            }

            if (prefix == PREFIX_CONTEXT)
            {
                ProcessInlineHighlights();

                _last = new Models.TextDiffLine(Models.TextDiffLineType.Normal, content, lineBytes.ToArray(), _oldLine, _newLine);
                _result.TextDiff.Lines.Add(_last);
                _oldLine++;
                _newLine++;
                return true;
            }

            if (prefix == PREFIX_COMMAND)
            {
                if (content.Equals(SPECIAL_NO_NEWLINE, StringComparison.Ordinal))
                    _last.NoNewLineEndOfFile = true;
                return true;
            }

            return false;
        }

        private bool ParseFileModeChange(string line)
        {
            if (line.StartsWith(FILE_MODE_OLD, StringComparison.Ordinal))
            {
                _result.OldMode = int.Parse(line.AsSpan(9));
                return true;
            }

            if (line.StartsWith(FILE_MODE_NEW, StringComparison.Ordinal))
            {
                _result.NewMode = int.Parse(line.AsSpan(9));
                return true;
            }

            if (line.StartsWith(FILE_MODE_DELETED, StringComparison.Ordinal))
            {
                _result.OldMode = int.Parse(line.AsSpan(18));
                return true;
            }

            if (line.StartsWith(FILE_MODE_ADDED, StringComparison.Ordinal))
            {
                _result.NewMode = int.Parse(line.AsSpan(14));
                return true;
            }

            return false;
        }

        private bool ParseLFSChange(char prefix, string content)
        {
            if (_isLFS)
            {
                if (prefix == PREFIX_DELETED)
                {
                    if (content.StartsWith(LFS_OID_PREFIX, StringComparison.Ordinal))
                        _result.LFSDiff.Old.Oid = content.Substring(11);
                    else if (content.StartsWith(LFS_SIZE_PREFIX, StringComparison.Ordinal))
                        _result.LFSDiff.Old.Size = long.Parse(content.AsSpan(5));
                }
                else if (prefix == PREFIX_ADDED)
                {
                    if (content.StartsWith(LFS_OID_PREFIX, StringComparison.Ordinal))
                        _result.LFSDiff.New.Oid = content.Substring(11);
                    else if (content.StartsWith(LFS_SIZE_PREFIX, StringComparison.Ordinal))
                        _result.LFSDiff.New.Size = long.Parse(content.AsSpan(5));
                }
                else if (prefix == PREFIX_CONTEXT)
                {
                    if (content.StartsWith(LFS_SIZE_PREFIX, StringComparison.Ordinal))
                        _result.LFSDiff.New.Size = _result.LFSDiff.Old.Size = long.Parse(content.AsSpan(5));
                }
                return true;
            }

            if ((_oldLine == 1 && _newLine == 1 && prefix == PREFIX_CONTEXT) ||
                (_oldLine == 1 && _newLine == 0 && prefix == PREFIX_DELETED) ||
                (_oldLine == 0 && _newLine == 1 && prefix == PREFIX_ADDED))
            {
                if (content.StartsWith(LFS_SPECIFIER, StringComparison.Ordinal))
                {
                    _isLFS = true;
                    _result.LFSDiff = new Models.LFSDiff();
                    return true;
                }
            }

            return false;
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
        private bool _isInChunk = false;
        private bool _isLFS = false;
    }
}
