using System.Collections.Generic;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace SourceGit.Converters
{
    public static class BranchTreeNodeConverters
    {
        public static readonly CornerRadius DEFAULT = new CornerRadius(4);
        
        public static readonly FuncMultiValueConverter<object, CornerRadius> ToCornerRadius =
            new FuncMultiValueConverter<object, CornerRadius>(v =>
            {
                if (v == null)
                    return DEFAULT;

                var array = new List<object>();
                array.AddRange(v);
                if (array.Count != 2)
                    return DEFAULT;

                var item = array[1] as TreeViewItem;
                if (item == null || !item.IsSelected)
                    return DEFAULT;

                var prev = GetPrevTreeViewItem(item);
                var next = GetNextTreeViewItem(item, true);

                double top = 4, bottom = 4;
                if (prev != null && prev.IsSelected)
                    top = 0;
                if (next != null && next.IsSelected)
                    bottom = 0;

                return new CornerRadius(top, bottom);
            });

        private static TreeViewItem GetPrevTreeViewItem(TreeViewItem item)
        {
            if (item.Parent is TreeView tree)
            {
                var idx = tree.IndexFromContainer(item);
                if (idx == 0)
                    return null;

                var prev = tree.ContainerFromIndex(idx - 1) as TreeViewItem;
                if (prev != null && prev.IsExpanded && prev.ItemCount > 0)
                    return prev.ContainerFromIndex(prev.ItemCount - 1) as TreeViewItem;

                return prev;
            }
            else if (item.Parent is TreeViewItem parentItem)
            {
                var idx = parentItem.IndexFromContainer(item);
                if (idx == 0)
                    return parentItem;

                var prev = parentItem.ContainerFromIndex(idx - 1) as TreeViewItem;
                if (prev != null && prev.IsExpanded && prev.ItemCount > 0)
                    return prev.ContainerFromIndex(prev.ItemCount - 1) as TreeViewItem;

                return prev;
            }
            else
            {
                return null;
            }
        }

        private static TreeViewItem GetNextTreeViewItem(TreeViewItem item, bool intoSelf = false)
        {
            if (intoSelf && item.IsExpanded && item.ItemCount > 0)
                return item.ContainerFromIndex(0) as TreeViewItem;
            
            if (item.Parent is TreeView tree)
            {
                var idx = tree.IndexFromContainer(item);
                if (idx == tree.ItemCount - 1)
                    return null;

                return tree.ContainerFromIndex(idx + 1) as TreeViewItem;
            }
            else if (item.Parent is TreeViewItem parentItem)
            {
                var idx = parentItem.IndexFromContainer(item);
                if (idx == parentItem.ItemCount - 1)
                    return GetNextTreeViewItem(parentItem);
                
                return parentItem.ContainerFromIndex(idx + 1) as TreeViewItem;
            }
            else
            {
                return null;
            }
        }
    }
}
