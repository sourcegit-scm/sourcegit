using System.Collections.Generic;

using Avalonia.Collections;

namespace SourceGit.ViewModels
{
    public class ChangeCollectionAsTree
    {
        public List<ChangeTreeNode> Tree { get; set; } = new List<ChangeTreeNode>();
        public AvaloniaList<ChangeTreeNode> Rows { get; set; } = new AvaloniaList<ChangeTreeNode>();
    }

    public class ChangeCollectionAsGrid
    {
        public AvaloniaList<Models.Change> Changes { get; set; } = new AvaloniaList<Models.Change>();
    }

    public class ChangeCollectionAsList
    {
        public AvaloniaList<Models.Change> Changes { get; set; } = new AvaloniaList<Models.Change>();
    }
}
