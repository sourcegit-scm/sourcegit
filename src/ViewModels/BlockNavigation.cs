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

        public BlockNavigation(List<Models.TextDiffLine> lines)
        {
            Blocks.Clear();
            Current = -1;

            if (lines.Count == 0)
                return;

            var lineIdx = 0;
            var blockStartIdx = 0;
            var isNewBlock = true;
            var blocks = new List<Block>();

            foreach (var line in lines)
            {
                lineIdx++;
                if (line.Type is Models.TextDiffLineType.Added or Models.TextDiffLineType.Deleted or Models.TextDiffLineType.None)
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
            if (_current >= 0 && _current < Blocks.Count)
                return Blocks[_current];

            return Blocks.Count > 0 ? Blocks[0] : null;
        }

        public Block GotoFirst()
        {
            if (Blocks.Count == 0)
                return null;

            Current = 0;
            OnPropertyChanged(nameof(Indicator));
            return Blocks[_current];
        }

        public Block GotoPrev()
        {
            if (Blocks.Count == 0)
                return null;

            if (_current == -1)
                Current = 0;
            else if (_current > 0)
                Current = _current - 1;

            OnPropertyChanged(nameof(Indicator));
            return Blocks[_current];
        }

        public Block GotoNext()
        {
            if (Blocks.Count == 0)
                return null;

            if (_current < Blocks.Count - 1)
                Current = _current + 1;

            OnPropertyChanged(nameof(Indicator));
            return Blocks[_current];
        }

        public Block GotoLast()
        {
            if (Blocks.Count == 0)
                return null;

            Current = Blocks.Count - 1;
            OnPropertyChanged(nameof(Indicator));
            return Blocks[_current];
        }

        public void AutoUpdate(int start, int end)
        {
            if (_current >= 0 && _current < Blocks.Count)
            {
                var block = Blocks[_current];
                if ((block.Start >= start && block.Start <= end) ||
                    (block.End >= start && block.End <= end) ||
                    (block.Start <= start && block.End >= end))
                    return;
            }

            for (var i = 0; i < Blocks.Count; i++)
            {
                var block = Blocks[i];
                if ((block.Start >= start && block.Start <= end) ||
                    (block.End >= start && block.End <= end) ||
                    (block.Start <= start && block.End >= end))
                {
                    Current = i;
                    OnPropertyChanged(nameof(Indicator));
                    return;
                }
            }
        }

        private int _current = -1;
    }
}
