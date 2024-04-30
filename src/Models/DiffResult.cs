﻿using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

using Avalonia.Media.Imaging;

namespace SourceGit.Models
{
    public enum TextDiffLineType
    {
        None,
        Normal,
        Indicator,
        Added,
        Deleted,
    }

    public class TextInlineRange
    {
        public int Start { get; set; }
        public int Count { get; set; }
        public TextInlineRange(int p, int n) { Start = p; Count = n; }
    }

    public class TextDiffLine
    {
        public TextDiffLineType Type { get; set; } = TextDiffLineType.None;
        public string Content { get; set; } = "";
        public int OldLineNumber { get; set; } = 0;
        public int NewLineNumber { get; set; } = 0;
        public List<TextInlineRange> Highlights { get; set; } = new List<TextInlineRange>();

        public string OldLine => OldLineNumber == 0 ? string.Empty : OldLineNumber.ToString();
        public string NewLine => NewLineNumber == 0 ? string.Empty : NewLineNumber.ToString();

        public TextDiffLine() { }
        public TextDiffLine(TextDiffLineType type, string content, int oldLine, int newLine)
        {
            Type = type;
            Content = content;
            OldLineNumber = oldLine;
            NewLineNumber = newLine;
        }
    }

    public class TextDiffSelection
    {
        public int StartLine { get; set; } = 0;
        public int EndLine { get; set; } = 0;
        public bool HasChanges { get; set; } = false;
        public bool HasLeftChanges { get; set; } = false;
        public int IgnoredAdds { get; set; } = 0;
        public int IgnoredDeletes { get; set; } = 0;

        public bool IsInRange(int idx)
        {
            return idx >= StartLine - 1 && idx < EndLine;
        }
    }

    public partial class TextDiff
    {
        public string File { get; set; } = string.Empty;
        public List<TextDiffLine> Lines { get; set; } = new List<TextDiffLine>();
        public int MaxLineNumber = 0;

        public void GenerateNewPatchFromSelection(Change change, string fileBlobGuid, TextDiffSelection selection, bool revert, string output)
        {
            var isTracked = !string.IsNullOrEmpty(fileBlobGuid);
            var fileGuid = isTracked ? fileBlobGuid.Substring(0, 8) : "00000000";

            var builder = new StringBuilder();
            builder.Append("diff --git a/").Append(change.Path).Append(" b/").Append(change.Path).Append('\n');
            if (!revert && !isTracked)
                builder.Append("new file mode 100644\n");
            builder.Append("index 00000000...").Append(fileGuid).Append('\n');
            builder.Append("--- ").Append((revert || isTracked) ? $"a/{change.Path}\n" : "/dev/null\n");
            builder.Append("+++ b/").Append(change.Path).Append('\n');

            var additions = selection.EndLine - selection.StartLine;
            if (selection.StartLine != 1)
                additions++;

            if (revert)
            {
                var totalLines = Lines.Count - 1;
                builder.Append($"@@ -0,").Append(totalLines - additions).Append(" +0,").Append(totalLines).Append(" @@");
                for (int i = 1; i <= totalLines; i++)
                {
                    var line = Lines[i];
                    if (line.Type != TextDiffLineType.Added)
                        continue;
                    builder.Append(selection.IsInRange(i) ? "\n+" : "\n ").Append(line.Content);
                }
            }
            else
            {
                builder.Append("@@ -0,0 +0,").Append(additions).Append(" @@");
                for (int i = selection.StartLine - 1; i < selection.EndLine; i++)
                {
                    var line = Lines[i];
                    if (line.Type != TextDiffLineType.Added)
                        continue;
                    builder.Append("\n+").Append(line.Content);
                }
            }

            builder.Append("\n\\ No newline at end of file\n");
            System.IO.File.WriteAllText(output, builder.ToString());
        }

        public void GeneratePatchFromSelection(Change change, string fileTreeGuid, TextDiffSelection selection, bool revert, string output)
        {
            var orgFile = !string.IsNullOrEmpty(change.OriginalPath) ? change.OriginalPath : change.Path;

            var builder = new StringBuilder();
            builder.Append("diff --git a/").Append(change.Path).Append(" b/").Append(change.Path).Append('\n');
            builder.Append("index 00000000...").Append(fileTreeGuid).Append(" 100644\n");
            builder.Append("--- a/").Append(orgFile).Append('\n');
            builder.Append("+++ b/").Append(change.Path);

            // If last line of selection is a change. Find one more line.
            var tail = null as string;
            if (selection.EndLine < Lines.Count)
            {
                var lastLine = Lines[selection.EndLine - 1];
                if (lastLine.Type == TextDiffLineType.Added || lastLine.Type == TextDiffLineType.Deleted)
                {
                    for (int i = selection.EndLine; i < Lines.Count; i++)
                    {
                        var line = Lines[i];
                        if (line.Type == TextDiffLineType.Indicator)
                            break;
                        if (revert)
                        {
                            if (line.Type == TextDiffLineType.Normal || line.Type == TextDiffLineType.Added)
                            {
                                tail = line.Content;
                                break;
                            }
                        }
                        else
                        {
                            if (line.Type == TextDiffLineType.Normal || line.Type == TextDiffLineType.Deleted)
                            {
                                tail = line.Content;
                                break;
                            }
                        }
                    }
                }
            }

            // If the first line is not indicator.
            if (Lines[selection.StartLine - 1].Type != TextDiffLineType.Indicator)
            {
                var indicator = selection.StartLine - 1;
                for (int i = selection.StartLine - 2; i >= 0; i--)
                {
                    var line = Lines[i];
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
                    var line = Lines[i];
                    if (line.Type == TextDiffLineType.Added)
                    {
                        ignoreAdds++;
                    }
                    else if (line.Type == TextDiffLineType.Deleted)
                    {
                        ignoreRemoves++;
                    }
                }

                for (int i = indicator; i < selection.StartLine - 1; i++)
                {
                    var line = Lines[i];
                    if (line.Type == TextDiffLineType.Indicator)
                    {
                        ProcessIndicatorForPatch(builder, line, i, selection.StartLine, selection.EndLine, ignoreRemoves, ignoreAdds, revert, tail != null);
                    }
                    else if (line.Type == TextDiffLineType.Added)
                    {
                        if (revert)
                            builder.Append("\n ").Append(line.Content);
                    }
                    else if (line.Type == TextDiffLineType.Deleted)
                    {
                        if (!revert)
                            builder.Append("\n ").Append(line.Content);
                    }
                    else if (line.Type == TextDiffLineType.Normal)
                    {
                        builder.Append("\n ").Append(line.Content);
                    }
                }
            }

            // Outputs the selected lines.
            for (int i = selection.StartLine - 1; i < selection.EndLine; i++)
            {
                var line = Lines[i];
                if (line.Type == TextDiffLineType.Indicator)
                {
                    if (!ProcessIndicatorForPatch(builder, line, i, selection.StartLine, selection.EndLine, selection.IgnoredDeletes, selection.IgnoredAdds, revert, tail != null))
                    {
                        break;
                    }
                }
                else if (line.Type == TextDiffLineType.Normal)
                {
                    builder.Append("\n ").Append(line.Content);
                }
                else if (line.Type == TextDiffLineType.Added)
                {
                    builder.Append("\n+").Append(line.Content);
                }
                else if (line.Type == TextDiffLineType.Deleted)
                {
                    builder.Append("\n-").Append(line.Content);
                }
            }

            builder.Append("\n ").Append(tail);
            builder.Append("\n");
            System.IO.File.WriteAllText(output, builder.ToString());
        }

        public void GeneratePatchFromSelectionSingleSide(Change change, string fileTreeGuid, TextDiffSelection selection, bool revert, bool isOldSide, string output)
        {
            var orgFile = !string.IsNullOrEmpty(change.OriginalPath) ? change.OriginalPath : change.Path;

            var builder = new StringBuilder();
            builder.Append("diff --git a/").Append(change.Path).Append(" b/").Append(change.Path).Append('\n');
            builder.Append("index 00000000...").Append(fileTreeGuid).Append(" 100644\n");
            builder.Append("--- a/").Append(orgFile).Append('\n');
            builder.Append("+++ b/").Append(change.Path);

            // If last line of selection is a change. Find one more line.
            var tail = null as string;
            if (selection.EndLine < Lines.Count)
            {
                var lastLine = Lines[selection.EndLine - 1];
                if (lastLine.Type == TextDiffLineType.Added || lastLine.Type == TextDiffLineType.Deleted)
                {
                    for (int i = selection.EndLine; i < Lines.Count; i++)
                    {
                        var line = Lines[i];
                        if (line.Type == TextDiffLineType.Indicator)
                            break;
                        if (revert)
                        {
                            if (line.Type == TextDiffLineType.Normal || line.Type == TextDiffLineType.Added)
                            {
                                tail = line.Content;
                                break;
                            }
                        }
                        else
                        {
                            if (line.Type == TextDiffLineType.Normal || line.Type == TextDiffLineType.Deleted)
                            {
                                tail = line.Content;
                                break;
                            }
                        }
                    }
                }
            }

            // If the first line is not indicator.
            if (Lines[selection.StartLine - 1].Type != TextDiffLineType.Indicator)
            {
                var indicator = selection.StartLine - 1;
                for (int i = selection.StartLine - 2; i >= 0; i--)
                {
                    var line = Lines[i];
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
                    var line = Lines[i];
                    if (line.Type == TextDiffLineType.Added)
                    {
                        ignoreAdds++;
                    }
                    else if (line.Type == TextDiffLineType.Deleted)
                    {
                        ignoreRemoves++;
                    }
                }

                for (int i = indicator; i < selection.StartLine - 1; i++)
                {
                    var line = Lines[i];
                    if (line.Type == TextDiffLineType.Indicator)
                    {
                        ProcessIndicatorForPatchSingleSide(builder, line, i, selection.StartLine, selection.EndLine, ignoreRemoves, ignoreAdds, revert, isOldSide, tail != null);
                    }
                    else if (line.Type == TextDiffLineType.Added)
                    {
                        if (revert)
                            builder.Append("\n ").Append(line.Content);
                    }
                    else if (line.Type == TextDiffLineType.Deleted)
                    {
                        if (!revert)
                            builder.Append("\n ").Append(line.Content);
                    }
                    else if (line.Type == TextDiffLineType.Normal)
                    {
                        builder.Append("\n ").Append(line.Content);
                    }
                }
            }

            // Outputs the selected lines.
            for (int i = selection.StartLine - 1; i < selection.EndLine; i++)
            {
                var line = Lines[i];
                if (line.Type == TextDiffLineType.Indicator)
                {
                    if (!ProcessIndicatorForPatchSingleSide(builder, line, i, selection.StartLine, selection.EndLine, selection.IgnoredDeletes, selection.IgnoredAdds, revert, isOldSide, tail != null))
                    {
                        break;
                    }
                }
                else if (line.Type == TextDiffLineType.Normal)
                {
                    builder.Append("\n ").Append(line.Content);
                }
                else if (line.Type == TextDiffLineType.Added)
                {
                    if (isOldSide)
                    {
                        if (revert)
                        {
                            builder.Append("\n ").Append(line.Content);
                        }
                        else
                        {
                            selection.IgnoredAdds++;
                        }
                    }
                    else
                    {
                        builder.Append("\n+").Append(line.Content);
                    }
                }
                else if (line.Type == TextDiffLineType.Deleted)
                {
                    if (isOldSide)
                    {
                        builder.Append("\n-").Append(line.Content);
                    }
                    else
                    {
                        if (!revert)
                        {
                            builder.Append("\n ").Append(line.Content);
                        }
                        else
                        {
                            selection.IgnoredDeletes++;
                        }
                    }
                }
            }

            builder.Append("\n ").Append(tail);
            builder.Append("\n");
            System.IO.File.WriteAllText(output, builder.ToString());
        }

        [GeneratedRegex(@"^@@ \-(\d+),?\d* \+(\d+),?\d* @@")]
        private static partial Regex REG_INDICATOR();

        private bool ProcessIndicatorForPatch(StringBuilder builder, TextDiffLine indicator, int idx, int start, int end, int ignoreRemoves, int ignoreAdds, bool revert, bool tailed)
        {
            var match = REG_INDICATOR().Match(indicator.Content);
            var oldStart = int.Parse(match.Groups[1].Value);
            var newStart = int.Parse(match.Groups[2].Value) + ignoreRemoves - ignoreAdds;
            var oldCount = 0;
            var newCount = 0;
            for (int i = idx + 1; i < end; i++)
            {
                var test = Lines[i];
                if (test.Type == TextDiffLineType.Indicator)
                    break;

                if (test.Type == TextDiffLineType.Normal)
                {
                    oldCount++;
                    newCount++;
                }
                else if (test.Type == TextDiffLineType.Added)
                {
                    if (i < start - 1)
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

                    if (i == end - 1 && tailed)
                    {
                        newCount++;
                        oldCount++;
                    }
                }
                else if (test.Type == TextDiffLineType.Deleted)
                {
                    if (i < start - 1)
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

                    if (i == end - 1 && tailed)
                    {
                        newCount++;
                        oldCount++;
                    }
                }
            }

            if (oldCount == 0 && newCount == 0)
                return false;

            builder.Append($"\n@@ -{oldStart},{oldCount} +{newStart},{newCount} @@");
            return true;
        }

        private bool ProcessIndicatorForPatchSingleSide(StringBuilder builder, TextDiffLine indicator, int idx, int start, int end, int ignoreRemoves, int ignoreAdds, bool revert, bool isOldSide, bool tailed)
        {
            var match = REG_INDICATOR().Match(indicator.Content);
            var oldStart = int.Parse(match.Groups[1].Value);
            var newStart = int.Parse(match.Groups[2].Value) + ignoreRemoves - ignoreAdds;
            var oldCount = 0;
            var newCount = 0;
            for (int i = idx + 1; i < end; i++)
            {
                var test = Lines[i];
                if (test.Type == TextDiffLineType.Indicator)
                    break;

                if (test.Type == TextDiffLineType.Normal)
                {
                    oldCount++;
                    newCount++;
                }
                else if (test.Type == TextDiffLineType.Added)
                {
                    if (i < start - 1)
                    {
                        if (revert)
                        {
                            newCount++;
                            oldCount++;
                        }
                    }
                    else
                    {
                        if (isOldSide)
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
                    }

                    if (i == end - 1 && tailed)
                    {
                        newCount++;
                        oldCount++;
                    }
                }
                else if (test.Type == TextDiffLineType.Deleted)
                {
                    if (i < start - 1)
                    {
                        if (!revert)
                        {
                            newCount++;
                            oldCount++;
                        }
                    }
                    else
                    {
                        if (isOldSide)
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

                    if (i == end - 1 && tailed)
                    {
                        newCount++;
                        oldCount++;
                    }
                }
            }

            if (oldCount == 0 && newCount == 0)
                return false;

            builder.Append($"\n@@ -{oldStart},{oldCount} +{newStart},{newCount} @@");
            return true;
        }
    }

    public class LFSDiff
    {
        public LFSObject Old { get; set; } = new LFSObject();
        public LFSObject New { get; set; } = new LFSObject();
    }

    public class BinaryDiff
    {
        public long OldSize { get; set; } = 0;
        public long NewSize { get; set; } = 0;
    }

    public class ImageDiff
    {
        public Bitmap Old { get; set; } = null;
        public Bitmap New { get; set; } = null;

        public string OldSize => Old != null ? $"{Old.PixelSize.Width} x {Old.PixelSize.Height}" : "0 x 0";
        public string NewSize => New != null ? $"{New.PixelSize.Width} x {New.PixelSize.Height}" : "0 x 0";
    }

    public class NoOrEOLChange
    {
    }

    public class FileModeDiff
    {
        public string Old { get; set; } = string.Empty;
        public string New { get; set; } = string.Empty;
    }

    public class DiffResult
    {
        public bool IsBinary { get; set; } = false;
        public bool IsLFS { get; set; } = false;
        public string OldMode { get; set; } = string.Empty;
        public string NewMode { get; set; } = string.Empty;
        public TextDiff TextDiff { get; set; } = null;
        public LFSDiff LFSDiff { get; set; } = null;

        public string FileModeChange => string.IsNullOrEmpty(OldMode) ? string.Empty : $"{OldMode} → {NewMode}";
    }
}
