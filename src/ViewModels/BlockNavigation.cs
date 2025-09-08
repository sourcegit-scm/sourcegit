using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class BlockNavigation : ObservableObject
    {
        public record Block(int Start, int End)
        {
            public bool IsInRange(int line)
            {
                return line >= Start && line <= End;
            }
        }

        public string Indicator
        {
            get
            {
                if (_blocks.Count == 0)
                    return "-/-";

                if (_current >= 0 && _current < _blocks.Count)
                    return $"{_current + 1}/{_blocks.Count}";

                return $"-/{_blocks.Count}";
            }
        }

        public BlockNavigation(List<Models.TextDiffLine> lines)
        {
            _blocks.Clear();
            _current = -1;

            if (lines.Count == 0)
                return;

            var lineIdx = 0;
            var blockStartIdx = 0;
            var isReadingBlock = false;
            var blocks = new List<Block>();

            foreach (var line in lines)
            {
                lineIdx++;
                if (line.Type is Models.TextDiffLineType.Added or Models.TextDiffLineType.Deleted or Models.TextDiffLineType.None)
                {
                    if (!isReadingBlock)
                    {
                        isReadingBlock = true;
                        blockStartIdx = lineIdx;
                    }
                }
                else
                {
                    if (isReadingBlock)
                    {
                        blocks.Add(new Block(blockStartIdx, lineIdx - 1));
                        isReadingBlock = false;
                    }
                }
            }

            if (isReadingBlock)
                blocks.Add(new Block(blockStartIdx, lines.Count));

            _blocks.AddRange(blocks);
        }

        public Block GetCurrentBlock()
        {
            if (_current >= 0 && _current < _blocks.Count)
                return _blocks[_current];

            return null;
        }

        public Block GotoFirst()
        {
            if (_blocks.Count == 0)
                return null;

            _current = 0;
            OnPropertyChanged(nameof(Indicator));
            return _blocks[_current];
        }

        public Block GotoPrev()
        {
            if (_blocks.Count == 0)
                return null;

            if (_current == -1)
                _current = 0;
            else if (_current > 0)
                _current--;

            OnPropertyChanged(nameof(Indicator));
            return _blocks[_current];
        }

        public Block GotoNext()
        {
            if (_blocks.Count == 0)
                return null;

            if (_current < _blocks.Count - 1)
                _current++;

            OnPropertyChanged(nameof(Indicator));
            return _blocks[_current];
        }

        public Block GotoLast()
        {
            if (_blocks.Count == 0)
                return null;

            _current = _blocks.Count - 1;
            OnPropertyChanged(nameof(Indicator));
            return _blocks[_current];
        }

        public void UpdateByCaretPosition(int caretLine)
        {
            if (_current >= 0 && _current < _blocks.Count)
            {
                var block = _blocks[_current];
                if (block.IsInRange(caretLine))
                    return;
            }

            _current = -1;

            for (var i = 0; i < _blocks.Count; i++)
            {
                var block = _blocks[i];
                if (block.Start > caretLine)
                    break;

                _current = i;
                if (block.End >= caretLine)
                    break;
            }

            OnPropertyChanged(nameof(Indicator));
        }

        private int _current;
        private readonly List<Block> _blocks = [];
    }
}
