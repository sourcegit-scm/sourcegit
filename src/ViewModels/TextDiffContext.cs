using System;
using System.Collections.Generic;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public record TextDiffSelectedChunk(double y, double h, int start, int end, bool combined, bool isOldSide)
    {
        public double Y { get; set; } = y;
        public double Height { get; set; } = h;
        public int StartIdx { get; set; } = start;
        public int EndIdx { get; set; } = end;
        public bool Combined { get; set; } = combined;
        public bool IsOldSide { get; set; } = isOldSide;

        public static bool IsChanged(TextDiffSelectedChunk oldValue, TextDiffSelectedChunk newValue)
        {
            if (newValue == null)
                return oldValue != null;

            if (oldValue == null)
                return true;

            return Math.Abs(newValue.Y - oldValue.Y) > 0.001 ||
                Math.Abs(newValue.Height - oldValue.Height) > 0.001 ||
                newValue.StartIdx != oldValue.StartIdx ||
                newValue.EndIdx != oldValue.EndIdx ||
                newValue.Combined != oldValue.Combined ||
                newValue.IsOldSide != oldValue.IsOldSide;
        }
    }

    public record TextDiffDisplayRange(int start, int end)
    {
        public int Start { get; set; } = start;
        public int End { get; set; } = end;
    }

    public class TextDiffContext : ObservableObject
    {
        public Models.TextDiff Data => _data;
        public string File => _data.File;
        public bool IsUnstaged => _data.Option.IsUnstaged;
        public bool EnableChunkOption => _data.Option.WorkingCopyChange != null;

        public Vector ScrollOffset
        {
            get => _scrollOffset;
            set => SetProperty(ref _scrollOffset, value);
        }

        public BlockNavigation BlockNavigation
        {
            get => _blockNavigation;
            set => SetProperty(ref _blockNavigation, value);
        }

        public TextDiffDisplayRange DisplayRange
        {
            get => _displayRange;
            set => SetProperty(ref _displayRange, value);
        }

        public TextDiffSelectedChunk SelectedChunk
        {
            get => _selectedChunk;
            set => SetProperty(ref _selectedChunk, value);
        }

        public void ResetBlockNavigation(bool enabled)
        {
            if (!enabled)
                BlockNavigation = null;
            else if (_blockNavigation == null)
                BlockNavigation = CreateBlockNavigation();
        }

        public (int, int) FindRangeByIndex(List<Models.TextDiffLine> lines, int lineIdx)
        {
            var startIdx = -1;
            var endIdx = -1;

            var normalLineCount = 0;
            var modifiedLineCount = 0;

            for (int i = lineIdx; i >= 0; i--)
            {
                var line = lines[i];
                if (line.Type == Models.TextDiffLineType.Indicator)
                {
                    startIdx = i;
                    break;
                }

                if (line.Type == Models.TextDiffLineType.Normal)
                {
                    normalLineCount++;
                    if (normalLineCount >= 2)
                    {
                        startIdx = i;
                        break;
                    }
                }
                else
                {
                    normalLineCount = 0;
                    modifiedLineCount++;
                }
            }

            normalLineCount = lines[lineIdx].Type == Models.TextDiffLineType.Normal ? 1 : 0;
            for (int i = lineIdx + 1; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line.Type == Models.TextDiffLineType.Indicator)
                {
                    endIdx = i;
                    break;
                }

                if (line.Type == Models.TextDiffLineType.Normal)
                {
                    normalLineCount++;
                    if (normalLineCount >= 2)
                    {
                        endIdx = i;
                        break;
                    }
                }
                else
                {
                    normalLineCount = 0;
                    modifiedLineCount++;
                }
            }

            if (endIdx == -1)
                endIdx = lines.Count - 1;

            return modifiedLineCount > 0 ? (startIdx, endIdx) : (-1, -1);
        }

        public virtual bool IsSideBySide()
        {
            return false;
        }

        public virtual TextDiffContext SwitchMode()
        {
            return null;
        }

        public virtual BlockNavigation CreateBlockNavigation()
        {
            return new BlockNavigation(_data.Lines);
        }

        protected Models.TextDiff _data = null;
        protected Vector _scrollOffset = Vector.Zero;
        protected BlockNavigation _blockNavigation = null;
        protected TextDiffDisplayRange _displayRange = null;
        protected TextDiffSelectedChunk _selectedChunk = null;
    }

    public class CombinedTextDiff : TextDiffContext
    {
        public CombinedTextDiff(Models.TextDiff diff, bool hasBlockNavigation, CombinedTextDiff previous = null)
        {
            _data = diff;

            if (previous != null && previous.File.Equals(File, StringComparison.Ordinal))
                _scrollOffset = previous.ScrollOffset;

            if (hasBlockNavigation)
                _blockNavigation = CreateBlockNavigation();
        }

        public override TextDiffContext SwitchMode()
        {
            return new TwoSideTextDiff(_data, _blockNavigation != null);
        }
    }

    public class TwoSideTextDiff : TextDiffContext
    {
        public List<Models.TextDiffLine> Old { get; } = new List<Models.TextDiffLine>();
        public List<Models.TextDiffLine> New { get; } = new List<Models.TextDiffLine>();

        public TwoSideTextDiff(Models.TextDiff diff, bool hasBlockNavigation, TwoSideTextDiff previous = null)
        {
            _data = diff;

            foreach (var line in diff.Lines)
            {
                switch (line.Type)
                {
                    case Models.TextDiffLineType.Added:
                        New.Add(line);
                        break;
                    case Models.TextDiffLineType.Deleted:
                        Old.Add(line);
                        break;
                    default:
                        FillEmptyLines();
                        Old.Add(line);
                        New.Add(line);
                        break;
                }
            }

            FillEmptyLines();

            if (previous != null && previous.File.Equals(File, StringComparison.Ordinal))
                _scrollOffset = previous._scrollOffset;

            if (hasBlockNavigation)
                _blockNavigation = CreateBlockNavigation();
        }

        public override bool IsSideBySide()
        {
            return true;
        }

        public override TextDiffContext SwitchMode()
        {
            return new CombinedTextDiff(_data, _blockNavigation != null);
        }

        public override BlockNavigation CreateBlockNavigation()
        {
            return new BlockNavigation(Old);
        }

        public void ConvertsToCombinedRange(ref int startLine, ref int endLine, bool isOldSide)
        {
            endLine = Math.Min(endLine, _data.Lines.Count - 1);

            var oneSide = isOldSide ? Old : New;
            var firstContentLine = -1;
            for (int i = startLine; i <= endLine; i++)
            {
                var line = oneSide[i];
                if (line.Type != Models.TextDiffLineType.None)
                {
                    firstContentLine = i;
                    break;
                }
            }

            if (firstContentLine < 0)
                return;

            var endContentLine = -1;
            for (int i = Math.Min(endLine, oneSide.Count - 1); i >= startLine; i--)
            {
                var line = oneSide[i];
                if (line.Type != Models.TextDiffLineType.None)
                {
                    endContentLine = i;
                    break;
                }
            }

            if (endContentLine < 0)
                return;

            var firstContent = oneSide[firstContentLine];
            var endContent = oneSide[endContentLine];
            startLine = _data.Lines.IndexOf(firstContent);
            endLine = _data.Lines.IndexOf(endContent);
        }

        private void FillEmptyLines()
        {
            if (Old.Count < New.Count)
            {
                int diff = New.Count - Old.Count;
                for (int i = 0; i < diff; i++)
                    Old.Add(new Models.TextDiffLine());
            }
            else if (Old.Count > New.Count)
            {
                int diff = Old.Count - New.Count;
                for (int i = 0; i < diff; i++)
                    New.Add(new Models.TextDiffLine());
            }
        }
    }
}
