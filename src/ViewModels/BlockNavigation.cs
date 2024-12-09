using System.Collections.Generic;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class BlockNavigation : ObservableObject
    {
        public class Block
        {
            public int Start { get; set; } = 0;
            public int End { get; set; } = 0;

            public Block(int start, int end)
            {
                Start = start;
                End = end;
            }

            public bool IsInRange(int line)
            {
                return line >= Start && line <= End;
            }
        }

        public AvaloniaList<Block> Blocks
        {
            get;
        } = [];

        public int Current
        {
            get => _current;
            private set => SetProperty(ref _current, value);
        }

        public string Indicator
        {
            get
            {
                if (Blocks.Count == 0)
                    return "-/-";

                if (_current >= 0 && _current < Blocks.Count)
                    return $"{_current + 1}/{Blocks.Count}";

                return $"-/{Blocks.Count}";
            }
        }

        public BlockNavigation(object context)
        {
            Blocks.Clear();
            Current = -1;

            var lines = new List<Models.TextDiffLine>();
            if (context is Models.TextDiff combined)
                lines = combined.Lines;
            else if (context is TwoSideTextDiff twoSide)
                lines = twoSide.Old;

            if (lines.Count == 0)
                return;

            var lineIdx = 0;
            var blockStartIdx = 0;
            var isNewBlock = true;
            var blocks = new List<Block>();

            foreach (var line in lines)
            {
                lineIdx++;
                if (line.Type == Models.TextDiffLineType.Added ||
                    line.Type == Models.TextDiffLineType.Deleted ||
                    line.Type == Models.TextDiffLineType.None)
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
                        blocks.Add(new Block(blockStartIdx, lineIdx - 1));
                        isNewBlock = true;
                    }
                }
            }

            if (!isNewBlock)
                blocks.Add(new Block(blockStartIdx, lines.Count - 1));

            Blocks.AddRange(blocks);
        }

        public Block GetCurrentBlock()
        {
            return (_current >= 0 && _current < Blocks.Count) ? Blocks[_current] : null;
        }

        public Block GotoNext()
        {
            if (Blocks.Count == 0)
                return null;

            Current = (_current + 1) % Blocks.Count;
            return Blocks[_current];
        }

        public Block GotoPrev()
        {
            if (Blocks.Count == 0)
                return null;

            Current = _current == -1 ? Blocks.Count - 1 : (_current - 1 + Blocks.Count) % Blocks.Count;
            return Blocks[_current];
        }

        private int _current = -1;
    }
}
