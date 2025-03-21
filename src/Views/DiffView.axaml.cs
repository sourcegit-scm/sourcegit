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
            if (sender is TextDiffView { UseBlockNavigation: true } textDiff)
                BlockNavigationIndicator.Text = textDiff.BlockNavigation?.Indicator ?? string.Empty;
        }
    }
}
