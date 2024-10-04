using System.ComponentModel;
using Avalonia.Controls;

namespace SourceGit.Views
{
    public static class ContextMenuExtension
    {
        public static void OpenContextMenu(this Control control, ContextMenu menu)
        {
            if (menu == null)
                return;

            menu.PlacementTarget = control;
            menu.Closing += OnContextMenuClosing; // Clear context menu because it is dynamic.

            control.ContextMenu = menu;
            control.ContextMenu?.Open();
        }

        private static void OnContextMenuClosing(object sender, CancelEventArgs e)
        {
            if (sender is ContextMenu menu && menu.PlacementTarget != null)
                menu.PlacementTarget.ContextMenu = null;
        }
    }
}
