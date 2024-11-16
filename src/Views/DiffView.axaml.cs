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
            if (ViewModels.Preference.Instance.EnableChangeBlocks)
            {
                if (DataContext is ViewModels.DiffContext diffCtx)
                    diffCtx.PrevChange();
            }
            else
            {
                var textDiff = this.FindDescendantOfType<ThemedTextDiffPresenter>();
                if (textDiff == null)
                    return;

                textDiff.GotoPrevChange();
                if (textDiff is SingleSideTextDiffPresenter presenter)
                    presenter.ForceSyncScrollOffset();

                e.Handled = true;
            }
        }

        private void OnGotoNextChange(object _, RoutedEventArgs e)
        {
            if (ViewModels.Preference.Instance.EnableChangeBlocks)
            {
                if (DataContext is ViewModels.DiffContext diffCtx)
                    diffCtx.NextChange();
            }
            else
            {
                var textDiff = this.FindDescendantOfType<ThemedTextDiffPresenter>();
                if (textDiff == null)
                    return;

                textDiff.GotoNextChange();
                if (textDiff is SingleSideTextDiffPresenter presenter)
                    presenter.ForceSyncScrollOffset();

                e.Handled = true;
            }
        }
    }
}
