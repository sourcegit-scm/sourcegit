using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class CommitDetail : UserControl
    {
        public CommitDetail()
        {
            InitializeComponent();
        }

        private void OnChangeListContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.CommitDetail vm && sender is ChangeCollectionView view)
            {
                var selected = view.SelectedChanges;
                if (selected != null && selected.Count == 1)
                {
                    var menu = vm.CreateChangeContextMenu(selected[0]);
                    view.OpenContextMenu(menu);
                }
            }
            e.Handled = true;
        }

        private void OnChangeDoubleTapped(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.CommitDetail vm && sender is ChangeCollectionView view)
            {
                var selected = view.SelectedChanges;
                if (selected != null && selected.Count == 1)
                {
                    vm.ActivePageIndex = 1;
                    vm.SelectedChanges = new() { selected[0] };
                }
            }
            e.Handled = true;
        }
    }
}
