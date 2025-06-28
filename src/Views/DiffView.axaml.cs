using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public partial class DiffView : UserControl
    {
        public DiffView()
        {
            InitializeComponent();
        }

        private void OnGotoFirstChange(object _, RoutedEventArgs e)
        {
            this.FindDescendantOfType<TextDiffView>()?.GotoFirstChange();
            e.Handled = true;
        }

        private void OnGotoPrevChange(object _, RoutedEventArgs e)
        {
            this.FindDescendantOfType<TextDiffView>()?.GotoPrevChange();
            e.Handled = true;
        }

        private void OnGotoNextChange(object _, RoutedEventArgs e)
        {
            this.FindDescendantOfType<TextDiffView>()?.GotoNextChange();
            e.Handled = true;
        }

        private void OnGotoLastChange(object _, RoutedEventArgs e)
        {
            this.FindDescendantOfType<TextDiffView>()?.GotoLastChange();
            e.Handled = true;
        }

        private void OnBlockNavigationChanged(object sender, RoutedEventArgs e)
        {
            if (sender is TextDiffView textDiff)
                BlockNavigationIndicator.Text = textDiff.BlockNavigation?.Indicator ?? string.Empty;
        }

        private void OnUseFullTextDiffClicked(object sender, RoutedEventArgs e)
        {
            var textDiffView = this.FindDescendantOfType<TextDiffView>();

            var presenter = textDiffView?.FindDescendantOfType<ThemedTextDiffPresenter>();
            if (presenter == null)
                return;

            if (presenter.DataContext is Models.TextDiff combined)
                combined.ScrollOffset = Vector.Zero;
            else if (presenter.DataContext is ViewModels.TwoSideTextDiff twoSides)
                twoSides.File = string.Empty; // Just to reset `SyncScrollOffset` without affect UI refresh.

            (DataContext as ViewModels.DiffContext)?.ToggleFullTextDiff();
            e.Handled = true;
        }
    }
}
