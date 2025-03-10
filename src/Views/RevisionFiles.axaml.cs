using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class RevisionFiles : UserControl
    {
        public RevisionFiles()
        {
            InitializeComponent();
        }

        private void OnSearchBoxKeyDown(object _, KeyEventArgs e)
        {
            var vm = DataContext as ViewModels.CommitDetail;
            if (vm == null)
                return;

            if (e.Key == Key.Enter)
            {
                FileTree.SetSearchResult(vm.RevisionFileSearchFilter);
                e.Handled = true;
            }
            else if (e.Key == Key.Down || e.Key == Key.Up)
            {
                if (vm.RevisionFileSearchSuggestion.Count > 0)
                {
                    SearchSuggestionBox.Focus(NavigationMethod.Tab);
                    SearchSuggestionBox.SelectedIndex = 0;
                }

                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                vm.CancelRevisionFileSuggestions();
                e.Handled = true;
            }
        }

        private void OnSearchBoxTextChanged(object _, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(TxtSearchRevisionFiles.Text))
                FileTree.SetSearchResult(null);
        }

        private void OnSearchSuggestionBoxKeyDown(object _, KeyEventArgs e)
        {
            var vm = DataContext as ViewModels.CommitDetail;
            if (vm == null)
                return;

            if (e.Key == Key.Escape)
            {
                vm.CancelRevisionFileSuggestions();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && SearchSuggestionBox.SelectedItem is string content)
            {
                vm.RevisionFileSearchFilter = content;
                TxtSearchRevisionFiles.CaretIndex = content.Length;
                FileTree.SetSearchResult(vm.RevisionFileSearchFilter);
                e.Handled = true;
            }
        }

        private void OnSearchSuggestionDoubleTapped(object sender, TappedEventArgs e)
        {
            var vm = DataContext as ViewModels.CommitDetail;
            if (vm == null)
                return;

            var content = (sender as StackPanel)?.DataContext as string;
            if (!string.IsNullOrEmpty(content))
            {
                vm.RevisionFileSearchFilter = content;
                TxtSearchRevisionFiles.CaretIndex = content.Length;
                FileTree.SetSearchResult(vm.RevisionFileSearchFilter);
            }

            e.Handled = true;
        }
    }
}
