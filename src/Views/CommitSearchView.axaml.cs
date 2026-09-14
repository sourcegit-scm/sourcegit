using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class CommitSearchView : UserControl
    {
        public CommitSearchView()
        {
            InitializeComponent();
        }

        private void OnSearchBoxKeyDown(object _, KeyEventArgs e)
        {
            if (DataContext is not ViewModels.SearchCommitContext ctx)
                return;

            if (e.Key == Key.Enter)
            {
                ctx.StartSearch();
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                if (ctx.Suggestions is { Count: > 0 })
                {
                    SearchSuggestionBox.Focus(NavigationMethod.Tab);
                    SearchSuggestionBox.SelectedIndex = 0;
                }

                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                ctx.ClearSuggestions();
                e.Handled = true;
            }
        }

        private void OnSearchSuggestionBoxKeyDown(object _, KeyEventArgs e)
        {
            if (DataContext is not ViewModels.SearchCommitContext ctx)
                return;

            if (e.Key == Key.Escape)
            {
                ctx.ClearSuggestions();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                var selected = SearchSuggestionBox.SelectedItem;
                if (selected is string content)
                {
                    ctx.Filter = content;
                    TxtSearchCommitsBox.CaretIndex = content.Length;
                }
                else if (selected is Models.User user)
                {
                    var apply = user.ToString().EscapeForBRE();
                    ctx.Filter = apply;
                    TxtSearchCommitsBox.CaretIndex = apply.Length;
                }

                ctx.StartSearch();
                e.Handled = true;
            }
        }

        private void OnSearchSuggestionTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is not ViewModels.SearchCommitContext ctx)
                return;

            var selected = (sender as Control)?.DataContext;
            if (selected is string content)
            {
                ctx.Filter = content;
                TxtSearchCommitsBox.CaretIndex = content.Length;
            }
            else if (selected is Models.User user)
            {
                var apply = user.ToString().EscapeForBRE();
                ctx.Filter = apply;
                TxtSearchCommitsBox.CaretIndex = apply.Length;
            }

            ctx.StartSearch();
            e.Handled = true;
        }
    }
}
