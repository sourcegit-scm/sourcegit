using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class ConventionalCommitMessageBuilder : ChromelessWindow
    {
        public ConventionalCommitMessageBuilder()
        {
            CloseOnESC = true;
            InitializeComponent();
        }

        private void OnApplyClicked(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.ConventionalCommitMessageBuilder builder)
            {
                if (builder.Apply())
                    Close();
            }

            e.Handled = true;
        }
    }
}
