using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class EditApiKey : ChromelessWindow
    {
        public EditApiKey()
        {
            CloseOnESC = true;
            DataContext = new AI.ApiKey();
            InitializeComponent();
        }

        public EditApiKey(AI.ApiKey apiKey)
        {
            CloseOnESC = true;
            DataContext = apiKey;
            InitializeComponent();
        }

        private void ApplyChange(object _1, RoutedEventArgs _2)
        {
            if (DataContext is not AI.ApiKey apiKey)
            {
                Close(false);
                return;
            }

            if (!apiKey.IsValidStorage)
                return;

            Close(true);
        }

        private void CloseWindow(object _1, RoutedEventArgs _2)
        {
            Close(false);
        }
    }
}
