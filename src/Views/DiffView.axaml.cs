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

        private void OnGotoPrevChange(object _, RoutedEventArgs e)
        {
            var textDiff = this.FindDescendantOfType<TextDiffView>();
            textDiff?.GotoPrevChange();
            e.Handled = true;
        }

        private void OnGotoNextChange(object _, RoutedEventArgs e)
        {
            var textDiff = this.FindDescendantOfType<TextDiffView>();
            textDiff?.GotoNextChange();
            e.Handled = true;
        }
    }
}
