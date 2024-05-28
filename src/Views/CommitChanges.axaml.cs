using Avalonia;
using Avalonia.Controls;

namespace SourceGit.Views
{
    public partial class CommitChanges : UserControl
    {
        public CommitChanges()
        {
            InitializeComponent();
        }

        private void OnChangeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.CommitDetail vm)
            {
                var selected = (sender as ChangeCollectionView)?.SelectedChanges;
                if (selected != null && selected.Count == 1)
                {
                    var menu = vm.CreateChangeContextMenu(selected[0]);
                    (sender as Control)?.OpenContextMenu(menu);
                }                
            }

            e.Handled = true;
        }
    }
}
