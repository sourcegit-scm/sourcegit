﻿using System;
using System.Collections.Generic;

using Avalonia;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class TwoSideTextDiff : ObservableObject
    {
        public string File { get; set; }
        public List<Models.TextDiffLine> Old { get; set; } = new List<Models.TextDiffLine>();
        public List<Models.TextDiffLine> New { get; set; } = new List<Models.TextDiffLine>();
        public int MaxLineNumber = 0;

        public Vector SyncScrollOffset
        {
            get => _syncScrollOffset;
            set => SetProperty(ref _syncScrollOffset, value);
        }

        public TwoSideTextDiff(Models.TextDiff diff, TwoSideTextDiff previous = null)
        {
            File = diff.File;
            MaxLineNumber = diff.MaxLineNumber;

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

            ProcessChangeBlocks();

            if (previous != null && previous.File == File)
                _syncScrollOffset = previous._syncScrollOffset;
        }

        public List<Models.TextDiffChangeBlock> ChangeBlocks { get; set; } = [];

        public void ProcessChangeBlocks()
        {
            ChangeBlocks.Clear();
            int lineIdx = 0, blockStartIdx = 0;
            bool isNewBlock = true;
            foreach (var line in Old) // NOTE: Same block size in both Old and New lines.
            {
                lineIdx++;
                if (line.Type == Models.TextDiffLineType.Added ||
                    line.Type == Models.TextDiffLineType.Deleted ||
                    line.Type == Models.TextDiffLineType.None) // Empty
                {
                    if (isNewBlock)
                    {
                        isNewBlock = false;
                        blockStartIdx = lineIdx;
                    }
                }
                else
                {
                    if (!isNewBlock)
                    {
                        ChangeBlocks.Add(new Models.TextDiffChangeBlock(blockStartIdx, lineIdx - 1));
                        isNewBlock = true;
                    }
                }
            }
        }

        public void ConvertsToCombinedRange(Models.TextDiff combined, ref int startLine, ref int endLine, bool isOldSide)
        {
            endLine = Math.Min(endLine, combined.Lines.Count - 1);

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
            startLine = combined.Lines.IndexOf(firstContent);
            endLine = combined.Lines.IndexOf(endContent);
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

        private Vector _syncScrollOffset = Vector.Zero;
    }
}
