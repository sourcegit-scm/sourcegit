using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class CommitBaseInfo : UserControl
    {
        public static readonly StyledProperty<bool> CanNavigateProperty =
            AvaloniaProperty.Register<CommitBaseInfo, bool>(nameof(CanNavigate), true);

        public bool CanNavigate
        {
            get => GetValue(CanNavigateProperty);
            set => SetValue(CanNavigateProperty, value);
        }

        public static readonly StyledProperty<string> MessageProperty =
            AvaloniaProperty.Register<CommitBaseInfo, string>(nameof(Message), string.Empty);

        public string Message
        {
            get => GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        public CommitBaseInfo()
        {
            InitializeComponent();
        }

        private void OnParentSHAPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is Control { DataContext: string sha } &&
                DataContext is ViewModels.CommitDetail detail &&
                CanNavigate)
            {
                detail.NavigateTo(sha);
            }

            e.Handled = true;
        }
    }
}
