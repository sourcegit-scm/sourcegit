using System;
using System.Collections.Generic;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public record TextDiffDisplayRange(int Start, int End);

    public record TextDiffSelectedChunk(double Y, double Height, int StartIdx, int EndIdx, bool Combined, bool IsOldSide)
    {
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

    public class TextDiffContext : ObservableObject
    {
        public Models.DiffOption Option => _option;
        public Models.TextDiff Data => _data;

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

        protected void TryKeepPrevState(TextDiffContext prev, List<Models.TextDiffLine> lines)
        {
            var fastTest = prev != null &&
                prev._option.IsUnstaged == _option.IsUnstaged &&
                prev._option.Path.Equals(_option.Path, StringComparison.Ordinal) &&
                prev._option.OrgPath.Equals(_option.OrgPath, StringComparison.Ordinal) &&
                prev._option.Revisions.Count == _option.Revisions.Count;

            if (!fastTest)
            {
                _blockNavigation = new BlockNavigation(lines, 0);
                return;
            }

            for (int i = 0; i < _option.Revisions.Count; i++)
            {
                if (!prev._option.Revisions[i].Equals(_option.Revisions[i], StringComparison.Ordinal))
                {
                    _blockNavigation = new BlockNavigation(lines, 0);
                    return;
                }
            }

            _blockNavigation = new BlockNavigation(lines, prev._blockNavigation.GetCurrentBlockIndex());
        }

        protected Models.DiffOption _option = null;
        protected Models.TextDiff _data = null;
        protected Vector _scrollOffset = Vector.Zero;
        protected BlockNavigation _blockNavigation = null;

        private TextDiffDisplayRange _displayRange = null;
        private TextDiffSelectedChunk _selectedChunk = null;
    }

    public class CombinedTextDiff : TextDiffContext
    {
        public CombinedTextDiff(Models.DiffOption option, Models.TextDiff diff, TextDiffContext previous = null)
        {
            _option = option;
            _data = diff;

            TryKeepPrevState(previous, _data.Lines);
        }

        public override TextDiffContext SwitchMode()
        {
            return new TwoSideTextDiff(_option, _data, this);
        }
    }

    public class TwoSideTextDiff : TextDiffContext
    {
        public List<Models.TextDiffLine> Old { get; } = [];
        public List<Models.TextDiffLine> New { get; } = [];

        public TwoSideTextDiff(Models.DiffOption option, Models.TextDiff diff, TextDiffContext previous = null)
        {
            _option = option;
            _data = diff;

            var sourceIndices = new int[diff.Lines.Count];

            for (var sourceIndex = 0; sourceIndex < diff.Lines.Count; sourceIndex++)
            {
                var line = diff.Lines[sourceIndex];
                switch (line.Type)
                {
                    case Models.TextDiffLineType.Added:
                        sourceIndices[New.Count] = sourceIndex;
                        New.Add(line);
                        break;
                    case Models.TextDiffLineType.Deleted:
                        sourceIndices[Old.Count] = sourceIndex;
                        Old.Add(line);
                        break;
                    default:
                        FillEmptyLines();
                        sourceIndices[Old.Count] = sourceIndex;
                        Old.Add(line);
                        New.Add(line);
                        break;
                }
            }

            FillEmptyLines();
            _sourceIndices = ResizeSourceIndices(sourceIndices);
            TryKeepPrevState(previous, Old);
        }

        public override bool IsSideBySide()
        {
            return true;
        }

        public override TextDiffContext SwitchMode()
        {
            return new CombinedTextDiff(_option, _data, this);
        }

        public int ConvertToCombined(int line)
        {
            return 0 <= line && line < _sourceIndices.Length ? _sourceIndices[line] : line;
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

        private int[] ResizeSourceIndices(int[] sourceIndices)
        {
            var length = Old.Count; // same as `New.Count`

            if (length == sourceIndices.Length)
                return sourceIndices;

            var resized = new int[length];
            Array.Copy(sourceIndices, resized, length);
            return resized;
        }

        private readonly int[] _sourceIndices;
    }
}
