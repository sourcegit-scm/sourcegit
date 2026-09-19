using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class DirectoryTreeNode : ObservableObject
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public bool IsRepository { get; set; }

        public int Depth { get; set; }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public List<DirectoryTreeNode> Children { get; } = [];

        public DirectoryTreeNode(string path, string name, bool isRepository)
        {
            Path = path;
            Name = name;
            IsRepository = isRepository;
        }
    }
}
