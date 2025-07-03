using System.Collections.Generic;
using System.IO;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class RevisionFileTreeNode : ObservableObject
    {
        public Models.Object Backend { get; set; } = null;
        public int Depth { get; set; } = 0;
        public List<RevisionFileTreeNode> Children { get; set; } = new List<RevisionFileTreeNode>();

        public string Name
        {
            get => Backend == null ? string.Empty : Path.GetFileName(Backend.Path);
        }

        public bool IsFolder
        {
            get => Backend?.Type == Models.ObjectType.Tree;
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        private bool _isExpanded = false;
    }
}
