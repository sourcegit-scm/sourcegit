using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public class ListBoxEx : ListBox
    {
        protected override Type StyleKeyOverride => typeof(ListBox);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Space)
                return;

            base.OnKeyDown(e);
        }

        protected void Select(object item)
        {
            SelectedItem = item;
            ScrollIntoView(item);
            ContainerFromItem(item)?.Focus();
        }
    }
}
