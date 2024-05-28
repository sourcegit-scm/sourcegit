using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class RevisionCompare : UserControl
    {
        public RevisionCompare()
        {
            InitializeComponent();
        }

        private void OnChangeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.RevisionCompare vm && sender is ChangeCollectionView view)
            {
                var menu = vm.CreateChangeContextMenu();
                view.OpenContextMenu(menu);
            }

            e.Handled = true;
        }

        private void OnPressedSHA(object sender, PointerPressedEventArgs e)
        {
            if (DataContext is ViewModels.RevisionCompare vm && sender is TextBlock block)
                vm.NavigateTo(block.Text);

            e.Handled = true;
        }
    }
}
