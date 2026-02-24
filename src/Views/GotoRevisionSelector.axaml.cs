using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class GotoRevisionSelector : ChromelessWindow
    {
        public GotoRevisionSelector()
        {
            CloseOnESC = true;
            InitializeComponent();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            RevisionList.Focus();
        }

        private void OnListKeyDown(object sender, KeyEventArgs e)
        {
            if (e is not { Key: Key.Enter, KeyModifiers: KeyModifiers.None })
                return;

            if (sender is not ListBox { SelectedItem: Models.Commit commit })
                return;

            Close(commit);
            e.Handled = true;
        }

        private void OnListItemTapped(object sender, TappedEventArgs e)
        {
            if (sender is not Control { DataContext: Models.Commit commit })
                return;

            Close(commit);
            e.Handled = true;
        }
    }
}

