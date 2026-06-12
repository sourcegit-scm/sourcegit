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

        private const string LFS_SPECIFIER = "version https://git-lfs.github.com/spec/";
        private const string LFS_OID_PREFIX = "oid sha256:";
        private const string LFS_SIZE_PREFIX = "size ";

        private enum Indicator
        {
            ChunkHeader = '@',
            Context = ' ',
            Old = '-',
            New = '+',
            Special = '\\',
        }

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
                if (_isInChunk)
                {
                    ProcessInlineHighlights();
                    _isInChunk = false;
                }

                _result.TextDiff.MaxLineNumber = Math.Max(_newLine, _oldLine);
                _result.TextDiff.OldMode = _result.OldMode;
                _result.TextDiff.NewMode = _result.NewMode;
                _result.TextDiff.OldHash = _result.OldHash;
                _result.TextDiff.NewHash = _result.NewHash;
            }

            return _result;
        }

        private void ParseLine(byte[] lineBytes)
        {
            var line = Encoding.UTF8.GetString(lineBytes);
            if (line.Length == 0)
                return;

            if (ParseChunkStartLine(line, lineBytes))
                return;

            if (ParseChunkBodyLine(line[0], line.Substring(1), lineBytes[1..]))
                return;

            ParseDiffHeaderLine(line);
        }

        private void ParseDiffHeaderLine(string line)
        {
            if (line.StartsWith("diff"))
                return;

            if (ParseFileModeChange(line))
                return;

            if (line.StartsWith("index"))
            {
                var match = REG_HASH_CHANGE().Match(line);
                if (match.Success)
                {
                    // NOTE: For a TypeChanged file we receive two full sets of diff-lines within
                    // the same diff output, indicating a 'deleted file' followed by a 'new file' .
                    // We then keep the oldest Old hash and the newest New hash.
                    if (string.IsNullOrEmpty(_result.OldHash))
                        _result.OldHash = match.Groups[1].Value;
                    _result.NewHash = match.Groups[2].Value;
                }
                return;
            }

            if (line.StartsWith("Binary", StringComparison.Ordinal))
                _result.IsBinary = true;
        }

        private bool ParseChunkStartLine(System.String line, byte[] lineBytes)
        {
            if (line[0] == (char)Indicator.ChunkHeader)
            {
                if (_isInChunk)
                {
                    ProcessInlineHighlights();
                    _isInChunk = false;
                }

                var match = REG_INDICATOR().Match(line);
                if (match.Success)
                {
                    _oldLine = int.Parse(match.Groups[1].Value);
                    _newLine = int.Parse(match.Groups[2].Value);
                    _last = new Models.TextDiffLine(Models.TextDiffLineType.Indicator, line, lineBytes, 0, 0);
                    _result.TextDiff.Lines.Add(_last);

                    _isInChunk = true;
                    return true;
                }
            }
            return false;
        }

        private bool ParseChunkBodyLine(char ch, string line, byte[] rawContent)
        {
            if (_isInChunk)
            {
                if (ParseLFSChange(ch, line))
                    return true;

                if (ch == (char)Indicator.Old)
                {
                    _result.TextDiff.DeletedLines++;
                    _last = new Models.TextDiffLine(Models.TextDiffLineType.Deleted, line, rawContent, _oldLine, 0);
                    _deleted.Add(_last);
                    _oldLine++;
                    return true;
                }

                if (ch == (char)Indicator.New)
                {
                    _result.TextDiff.AddedLines++;
                    _last = new Models.TextDiffLine(Models.TextDiffLineType.Added, line, rawContent, 0, _newLine);
                    _added.Add(_last);
                    _newLine++;
                    return true;
                }

                if (ch == (char)Indicator.Context)
                {
                    ProcessInlineHighlights();

                    _last = new Models.TextDiffLine(Models.TextDiffLineType.Normal, line, rawContent, _oldLine, _newLine);
                    _result.TextDiff.Lines.Add(_last);
                    _oldLine++;
                    _newLine++;
                    return true;
                }

                if (ch == (char)Indicator.Special)
                {
                    if (line.Equals(" No newline at end of file", StringComparison.Ordinal))
                        _last.NoNewLineEndOfFile = true;
                    return true;
                }
            }

            ProcessInlineHighlights();
            _isInChunk = false;
            return false;
        }

        private int ParseFileModeNumber(string fileModeStr)
        {
            int fileMode = 0;
            Int32.TryParse(fileModeStr, out fileMode);
            return fileMode;
        }

        private bool ParseFileModeChange(string line)
        {
            if (line.StartsWith("old mode ", StringComparison.Ordinal))
            {
                _result.OldMode = ParseFileMode(line.Substring(9));
                return true;
            }

            if (line.StartsWith("new mode ", StringComparison.Ordinal))
            {
                _result.NewMode = ParseFileMode(line.Substring(9));
                return true;
            }

            if (line.StartsWith("deleted file mode ", StringComparison.Ordinal))
            {
                _result.OldMode = ParseFileMode(line.Substring(18));
                return true;
            }

            if (line.StartsWith("new file mode ", StringComparison.Ordinal))
            {
                _result.NewMode = ParseFileMode(line.Substring(14));
                return true;
            }

            return false;
        }

        private int ParseFileMode(string content)
        {
            int mode = 0;
            int.TryParse(content, out mode);
            return mode;
        }

        private bool ParseLFSChange(char ch, string line)
        {
            if (_result.IsLFS)
            {
                if (ch == (char)Indicator.Old)
                {
                    if (line.StartsWith(LFS_OID_PREFIX, StringComparison.Ordinal))
                        _result.LFSDiff.Old.Oid = line.Substring(11);
                    else if (line.StartsWith(LFS_SIZE_PREFIX, StringComparison.Ordinal))
                        _result.LFSDiff.Old.Size = long.Parse(line.AsSpan(5));
                }
                else if (ch == (char)Indicator.New)
                {
                    if (line.StartsWith(LFS_OID_PREFIX, StringComparison.Ordinal))
                        _result.LFSDiff.New.Oid = line.Substring(11);
                    else if (line.StartsWith(LFS_SIZE_PREFIX, StringComparison.Ordinal))
                        _result.LFSDiff.New.Size = long.Parse(line.AsSpan(5));
                }
                else if (ch == (char)Indicator.Context)
                {
                    if (line.StartsWith(LFS_SIZE_PREFIX, StringComparison.Ordinal))
                        _result.LFSDiff.New.Size = _result.LFSDiff.Old.Size = long.Parse(line.AsSpan(5));
                }
                return true;
            }

            if (_result.TextDiff.Lines.Count != 1)
                return false;

            if ((_oldLine == 1 && _newLine == 1 && ch == (char)Indicator.Context) ||
                (_oldLine == 1 && _newLine == 0 && ch == (char)Indicator.Old) ||
                (_oldLine == 0 && _newLine == 1 && ch == (char)Indicator.New))
            {
                if (line.StartsWith(LFS_SPECIFIER, StringComparison.Ordinal))
                {
                    _result.IsLFS = true;
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
    }
}
