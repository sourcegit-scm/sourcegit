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

        public CommitBaseInfo()
        {
            InitializeComponent();
        }

        private void OnParentSHAPressed(object sender, PointerPressedEventArgs e)
        {
            if (DataContext is ViewModels.CommitDetail detail && CanNavigate)
                detail.NavigateTo((sender as Control).DataContext as string);

            e.Handled = true;
        }
    }
}
