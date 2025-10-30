using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class RevisionFiles : UserControl
    {
        public RevisionFiles()
        {
            InitializeComponent();
        }

        private async void OnSearchBoxKeyDown(object _, KeyEventArgs e)
        {
            if (DataContext is not ViewModels.CommitDetail vm)
                return;

            if (e.Key == Key.Enter)
            {
                await FileTree.SetSearchResultAsync(vm.RevisionFileSearchFilter);
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

        private async void OnSearchBoxTextChanged(object _, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(TxtSearchRevisionFiles.Text))
                await FileTree.SetSearchResultAsync(null);
        }

        private async void OnSearchSuggestionBoxKeyDown(object _, KeyEventArgs e)
        {
            if (DataContext is not ViewModels.CommitDetail vm)
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
                await FileTree.SetSearchResultAsync(vm.RevisionFileSearchFilter);
                e.Handled = true;
            }
        }

        private async void OnSearchSuggestionDoubleTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is not ViewModels.CommitDetail vm)
                return;

            var content = (sender as StackPanel)?.DataContext as string;
            if (!string.IsNullOrEmpty(content))
            {
                vm.RevisionFileSearchFilter = content;
                TxtSearchRevisionFiles.CaretIndex = content.Length;
                await FileTree.SetSearchResultAsync(vm.RevisionFileSearchFilter);
            }

            e.Handled = true;
        }

        private async void OnOpenFileWithDefaultEditor(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.CommitDetail { CanOpenRevisionFileWithDefaultEditor: true } vm)
                await vm.OpenRevisionFileAsync(vm.ViewRevisionFilePath, null);

            e.Handled = true;
        }
    }
}
