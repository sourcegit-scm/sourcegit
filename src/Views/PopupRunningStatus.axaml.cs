using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class PopupRunningStatus : UserControl
    {
        public static readonly StyledProperty<string> DescriptionProperty =
            AvaloniaProperty.Register<PopupRunningStatus, string>(nameof(Description));

        public string Description
        {
            get => GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public PopupRunningStatus()
        {
            InitializeComponent();
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            _isUnloading = true;
            base.OnUnloaded(e);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == IsVisibleProperty)
            {
                if (IsVisible && !_isUnloading)
                    StartAnim();
                else
                    StopAnim();
            }
        }

        private void StartAnim()
        {
            Icon.Content = new Path() { Classes = { "waiting" } };
            ProgressBar.IsIndeterminate = true;
        }

        private void StopAnim()
        {
            if (Icon.Content is Path path)
                path.Classes.Clear();
            Icon.Content = null;
            ProgressBar.IsIndeterminate = false;
        }

        private bool _isUnloading = false;
    }
}
