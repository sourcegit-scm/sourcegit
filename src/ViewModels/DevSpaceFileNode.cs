using System.Collections.Generic;

using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public sealed class DevSpaceFileNode : ObservableObject
    {
        public string Name { get; }
        public string RelativePath { get; }
        public bool IsDirectory { get; }
        public int Depth { get; }
        public Thickness Indent => new(Depth * 16, 0, 0, 0);
        public List<DevSpaceFileNode> Children { get; } = [];

        public Models.Change Change
        {
            get => _change;
            set
            {
                if (SetProperty(ref _change, value))
                {
                    OnPropertyChanged(nameof(State));
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        public Models.ChangeState State
        {
            get
            {
                if (_change == null)
                    return Models.ChangeState.None;

                return _change.WorkTree != Models.ChangeState.None
                    ? _change.WorkTree
                    : _change.Index;
            }
        }

        public string StatusText => State switch
        {
            Models.ChangeState.Modified => "M",
            Models.ChangeState.TypeChanged => "T",
            Models.ChangeState.Added => "A",
            Models.ChangeState.Deleted => "D",
            Models.ChangeState.Renamed => "R",
            Models.ChangeState.Copied => "C",
            Models.ChangeState.Untracked => "?",
            Models.ChangeState.Conflicted => "!",
            _ => string.Empty,
        };

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public DevSpaceFileNode(string name, string relativePath, bool isDirectory, int depth)
        {
            Name = name;
            RelativePath = relativePath;
            IsDirectory = isDirectory;
            Depth = depth;
        }

        private Models.Change _change;
        private bool _isExpanded;
    }
}
