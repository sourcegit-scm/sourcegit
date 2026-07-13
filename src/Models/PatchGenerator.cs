using System.IO;
using System.Text.RegularExpressions;

namespace SourceGit.Models
{
    public partial class PatchGenerator
    {
        [GeneratedRegex(@"^@@ \-(\d+),?\d* \+(\d+),?\d* @@")]
        private static partial Regex REG_INDICATOR();

        public bool IsValid => _selection != null;

        public PatchGenerator(DiffOption option, TextDiff diff, int startLine, int endLine, bool isCombined, bool isOldSide)
        {
            _targetFile = option.Path;
            _diff = diff;
            _isCombined = isCombined;
            _isOldSide = isOldSide;

            var lines = _diff.Lines;
            var hasChanges = false;

            var selection = new Selection();
            selection.StartLine = startLine;
            selection.EndLine = endLine;

            for (int i = 0; i < startLine; i++)
            {
                var line = lines[i];
                if (line.Type == TextDiffLineType.Added)
                    selection.IgnoredAdds++;
                else if (line.Type == TextDiffLineType.Deleted)
                    selection.IgnoredDeletes++;
            }

            for (int i = startLine; i <= endLine; i++)
            {
                var line = lines[i];
                if (line.Type == TextDiffLineType.Added)
                {
                    if (isCombined || !isOldSide)
                    {
                        hasChanges = true;
                        break;
                    }
                }
                else if (line.Type == TextDiffLineType.Deleted)
                {
                    if (isCombined || isOldSide)
                    {
                        hasChanges = true;
                        break;
                    }
                }
            }

            if (hasChanges)
                _selection = selection;
        }

        public void Generate(string saveTo, bool revert)
        {
            if (_selection == null)
                return;

            using var writer = CreateWriter(saveTo, revert);

            if (_isCombined)
                GenerateCombined(writer, revert);
            else
                GenerateForSingleSide(writer, revert);
        }

        private void GenerateCombined(StreamWriter writer, bool revert)
        {
            var lines = _diff.Lines;
            var tail = FindTail(revert);

            // If the first line is not indicator.
            if (lines[_selection.StartLine].Type != TextDiffLineType.Indicator)
            {
                var indicator = _selection.StartLine;
                for (int i = _selection.StartLine - 1; i >= 0; i--)
                {
                    var line = lines[i];
                    if (line.Type == TextDiffLineType.Indicator)
                    {
                        indicator = i;
                        break;
                    }
                }

                var ignoreAdds = 0;
                var ignoreRemoves = 0;
                for (int i = 0; i < indicator; i++)
                {
                    var line = lines[i];
                    if (line.Type == TextDiffLineType.Added)
                    {
                        ignoreAdds++;
                    }
                    else if (line.Type == TextDiffLineType.Deleted)
                    {
                        ignoreRemoves++;
                    }
                }

                for (int i = indicator; i < _selection.StartLine; i++)
                {
                    var line = lines[i];
                    if (line.Type == TextDiffLineType.Indicator)
                    {
                        ProcessIndicator(writer, line, i, ignoreRemoves, ignoreAdds, revert, tail != null);
                    }
                    else if (line.Type == TextDiffLineType.Added)
                    {
                        if (revert)
                            Append(writer, ' ', line);
                    }
                    else if (line.Type == TextDiffLineType.Deleted)
                    {
                        if (!revert)
                            Append(writer, ' ', line);
                    }
                    else if (line.Type == TextDiffLineType.Normal)
                    {
                        Append(writer, ' ', line);
                    }
                }
            }

            // Outputs the selected lines.
            for (int i = _selection.StartLine; i <= _selection.EndLine; i++)
            {
                var line = lines[i];
                if (line.Type == TextDiffLineType.Indicator)
                {
                    if (!ProcessIndicator(writer, line, i, _selection.IgnoredDeletes, _selection.IgnoredAdds, revert, tail != null))
                        break;
                }
                else if (line.Type == TextDiffLineType.Normal)
                {
                    Append(writer, ' ', line);
                }
                else if (line.Type == TextDiffLineType.Added)
                {
                    Append(writer, '+', line);
                }
                else if (line.Type == TextDiffLineType.Deleted)
                {
                    Append(writer, '-', line);
                }
            }

            if (tail != null)
                Append(writer, ' ', tail);
        }

        private void GenerateForSingleSide(StreamWriter writer, bool revert)
        {
            var lines = _diff.Lines;
            var tail = FindTail(revert);

            // If the first line is not indicator.
            if (lines[_selection.StartLine].Type != TextDiffLineType.Indicator)
            {
                var indicator = _selection.StartLine;
                for (int i = _selection.StartLine - 1; i >= 0; i--)
                {
                    var line = lines[i];
                    if (line.Type == TextDiffLineType.Indicator)
                    {
                        indicator = i;
                        break;
                    }
                }

                var ignoreAdds = 0;
                var ignoreRemoves = 0;
                for (int i = 0; i < indicator; i++)
                {
                    var line = lines[i];
                    if (line.Type == TextDiffLineType.Added)
                    {
                        ignoreAdds++;
                    }
                    else if (line.Type == TextDiffLineType.Deleted)
                    {
                        ignoreRemoves++;
                    }
                }

                for (int i = indicator; i < _selection.StartLine; i++)
                {
                    var line = lines[i];
                    if (line.Type == TextDiffLineType.Indicator)
                    {
                        ProcessIndicatorForSingleSide(writer, line, i, ignoreRemoves, ignoreAdds, revert, tail != null);
                    }
                    else if (line.Type == TextDiffLineType.Added)
                    {
                        if (revert)
                            Append(writer, ' ', line);
                    }
                    else if (line.Type == TextDiffLineType.Deleted)
                    {
                        if (!revert)
                            Append(writer, ' ', line);
                    }
                    else if (line.Type == TextDiffLineType.Normal)
                    {
                        Append(writer, ' ', line);
                    }
                }
            }

            // Outputs the selected lines.
            for (int i = _selection.StartLine; i <= _selection.EndLine; i++)
            {
                var line = lines[i];
                if (line.Type == TextDiffLineType.Indicator)
                {
                    if (!ProcessIndicatorForSingleSide(writer, line, i, _selection.IgnoredDeletes, _selection.IgnoredAdds, revert, tail != null))
                        break;
                }
                else if (line.Type == TextDiffLineType.Normal)
                {
                    Append(writer, ' ', line);
                }
                else if (line.Type == TextDiffLineType.Added)
                {
                    if (_isOldSide)
                    {
                        if (revert)
                            Append(writer, ' ', line);
                        else
                            _selection.IgnoredAdds++;
                    }
                    else
                    {
                        Append(writer, '+', line);
                    }
                }
                else if (line.Type == TextDiffLineType.Deleted)
                {
                    if (_isOldSide)
                    {
                        Append(writer, '-', line);
                    }
                    else
                    {
                        if (!revert)
                            Append(writer, ' ', line);
                        else
                            _selection.IgnoredDeletes++;
                    }
                }
            }

            if (tail != null)
                Append(writer, ' ', tail);
        }

        private StreamWriter CreateWriter(string saveTo, bool revert)
        {
            var writer = new StreamWriter(saveTo) { NewLine = "\n" };
            writer.WriteLine($"diff --git \"a/{_targetFile}\" \"b/{_targetFile}\"");

            if (_diff.OldMode == 0)
            {
                if (_diff.NewMode == 0 || revert)
                {
                    writer.WriteLine($"index {_diff.OldHash}..{_diff.NewHash}");
                    writer.WriteLine($"--- a/{_targetFile}");
                }
                else
                {
                    writer.WriteLine($"new file mode {_diff.NewMode}");
                    writer.WriteLine($"--- /dev/null");
                }
            }
            else if (_diff.NewMode == 0)
            {
                writer.WriteLine($"index {_diff.OldHash}..{_diff.NewHash} {_diff.OldMode}");
                writer.WriteLine($"--- a/{_targetFile}");
            }
            else
            {
                writer.WriteLine($"--- a/{_targetFile}");
            }

            writer.WriteLine($"+++ b/{_targetFile}");
            return writer;
        }

        private TextDiffLine FindTail(bool revert)
        {
            var lines = _diff.Lines;

            if (_selection.EndLine < lines.Count - 1)
            {
                var lastLine = lines[_selection.EndLine];
                if (lastLine.Type == TextDiffLineType.Added || lastLine.Type == TextDiffLineType.Deleted)
                {
                    for (int i = _selection.EndLine + 1; i < lines.Count; i++)
                    {
                        var line = lines[i];
                        if (line.Type == TextDiffLineType.Indicator)
                            break;

                        if (revert)
                        {
                            if (line.Type == TextDiffLineType.Normal || line.Type == TextDiffLineType.Added)
                                return line;
                        }
                        else
                        {
                            if (line.Type == TextDiffLineType.Normal || line.Type == TextDiffLineType.Deleted)
                                return line;
                        }
                    }
                }
            }

            return null;
        }

        private void Append(StreamWriter writer, char prefix, TextDiffLine line)
        {
            writer.Flush();

            writer.BaseStream.WriteByte((byte)prefix);
            writer.BaseStream.Write(line.RawContent);
            writer.BaseStream.WriteByte((byte)'\n');

            if (line.NoNewLineEndOfFile)
                writer.WriteLine("\\ No newline at end of file");
        }

        private bool ProcessIndicator(StreamWriter writer, TextDiffLine indicator, int idx, int ignoreRemoves, int ignoreAdds, bool revert, bool tailed)
        {
            var lines = _diff.Lines;
            var start = _selection.StartLine;
            var end = _selection.EndLine;
            var match = REG_INDICATOR().Match(indicator.Content);
            var oldStart = int.Parse(match.Groups[1].Value);
            var newStart = int.Parse(match.Groups[2].Value) + ignoreRemoves - ignoreAdds;
            var oldCount = 0;
            var newCount = 0;

            for (int i = idx + 1; i <= end; i++)
            {
                var test = lines[i];
                if (test.Type == TextDiffLineType.Indicator)
                    break;

                if (test.Type == TextDiffLineType.Normal)
                {
                    oldCount++;
                    newCount++;
                }
                else if (test.Type == TextDiffLineType.Added)
                {
                    if (i < start)
                    {
                        if (revert)
                        {
                            newCount++;
                            oldCount++;
                        }
                    }
                    else
                    {
                        newCount++;
                    }

                    if (i == end && tailed)
                    {
                        newCount++;
                        oldCount++;
                    }
                }
                else if (test.Type == TextDiffLineType.Deleted)
                {
                    if (i < start)
                    {
                        if (!revert)
                        {
                            newCount++;
                            oldCount++;
                        }
                    }
                    else
                    {
                        oldCount++;
                    }

                    if (i == end && tailed)
                    {
                        newCount++;
                        oldCount++;
                    }
                }
            }

            if (oldCount == 0 && newCount == 0)
                return false;

            writer.WriteLine($"@@ -{oldStart},{oldCount} +{newStart},{newCount} @@");
            return true;
        }

        private bool ProcessIndicatorForSingleSide(StreamWriter writer, TextDiffLine indicator, int idx, int ignoreRemoves, int ignoreAdds, bool revert, bool tailed)
        {
            var lines = _diff.Lines;
            var start = _selection.StartLine;
            var end = _selection.EndLine;
            var match = REG_INDICATOR().Match(indicator.Content);
            var oldStart = int.Parse(match.Groups[1].Value);
            var newStart = int.Parse(match.Groups[2].Value) + ignoreRemoves - ignoreAdds;
            var oldCount = 0;
            var newCount = 0;

            for (int i = idx + 1; i <= end; i++)
            {
                var test = lines[i];
                if (test.Type == TextDiffLineType.Indicator)
                    break;

                if (test.Type == TextDiffLineType.Normal)
                {
                    oldCount++;
                    newCount++;
                }
                else if (test.Type == TextDiffLineType.Added)
                {
                    if (i < start || _isOldSide)
                    {
                        if (revert)
                        {
                            newCount++;
                            oldCount++;
                        }
                    }
                    else
                    {
                        newCount++;
                    }

                    if (i == end && tailed)
                    {
                        newCount++;
                        oldCount++;
                    }
                }
                else if (test.Type == TextDiffLineType.Deleted)
                {
                    if (i < start)
                    {
                        if (!revert)
                        {
                            newCount++;
                            oldCount++;
                        }
                    }
                    else
                    {
                        if (_isOldSide)
                        {
                            oldCount++;
                        }
                        else
                        {
                            if (!revert)
                            {
                                newCount++;
                                oldCount++;
                            }
                        }
                    }

                    if (i == end && tailed)
                    {
                        newCount++;
                        oldCount++;
                    }
                }
            }

            if (oldCount == 0 && newCount == 0)
                return false;

            writer.WriteLine($"@@ -{oldStart},{oldCount} +{newStart},{newCount} @@");
            return true;
        }

        private class Selection
        {
            public int StartLine { get; set; } = 0;
            public int EndLine { get; set; } = 0;
            public int IgnoredAdds { get; set; } = 0;
            public int IgnoredDeletes { get; set; } = 0;
        }

        private string _targetFile = null;
        private TextDiff _diff = null;
        private bool _isCombined = false;
        private bool _isOldSide = false;
        private Selection _selection = null;
    }
}
