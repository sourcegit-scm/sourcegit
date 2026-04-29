using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public class ListBoxItemEx : ListBoxItem
    {
        protected override Type StyleKeyOverride => typeof(ListBoxItem);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Space)
                return;

            base.OnKeyDown(e);
        }
    }

    public class ListBoxEx : ListBox
    {
        protected override Type StyleKeyOverride => typeof(ListBox);

        protected override Control CreateContainerForItemOverride(object item, int index, object recycleKey)
        {
            return new ListBoxItemEx();
        }

        protected override bool NeedsContainerOverride(object item, int index, out object recycleKey)
        {
            return NeedsContainer<ListBoxItemEx>(item, out recycleKey);
        }

        protected void Select(object item)
        {
            SelectedItem = item;
            ScrollIntoView(item);
            ContainerFromItem(item)?.Focus();
        }
    }
}
