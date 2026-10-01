using System.Collections;
using System.Collections.Generic;

namespace SourceGit.ViewModels
{
    public class ChangeSelection
    {
        public List<Models.Change> Changes { get; }
        public bool IsSingleFolder { get; }
        public string SingleFolderPath { get; }
        public bool HasFolder { get; }

        public int Count => Changes.Count;

        public ChangeSelection(IList selected)
        {
            var changes = new List<Models.Change>();

            IsSingleFolder = false;
            SingleFolderPath = null;
            HasFolder = false;

            if (selected is { Count: > 0 })
            {
                foreach (var item in selected)
                {
                    if (item is Models.Change c)
                    {
                        changes.Add(c);
                    }
                    else if (item is ChangeTreeNode node)
                    {
                        CollectChangesInNode(changes, node);

                        if (node.IsFolder && !HasFolder)
                            HasFolder = true;
                    }
                }

                if (selected.Count == 1 && selected[0] is ChangeTreeNode { IsFolder: true } folder)
                {
                    IsSingleFolder = true;
                    SingleFolderPath = folder.FullPath;
                }
            }

            Changes = changes;
        }

        public bool IsChanged(ChangeSelection other)
        {
            if (other == null ||
                IsSingleFolder != other.IsSingleFolder ||
                SingleFolderPath != other.SingleFolderPath ||
                HasFolder != other.HasFolder ||
                Changes.Count != other.Changes.Count)
                return true;

            foreach (var c in other.Changes)
            {
                if (!Changes.Contains(c))
                    return true;
            }

            return false;
        }

        private static void CollectChangesInNode(List<Models.Change> outs, ChangeTreeNode node)
        {
            if (node.IsFolder)
            {
                foreach (var child in node.Children)
                    CollectChangesInNode(outs, child);
            }
            else
            {
                if (!outs.Contains(node.Change))
                    outs.Add(node.Change);
            }
        }
    }
}
