using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class PopupRunningStatus : UserControl
    {
        public static readonly DirectProperty<PopupRunningStatus, string> DescriptionProperty =
            AvaloniaProperty.RegisterDirect<PopupRunningStatus, string>(
                nameof(Description),
                static o => o.Description,
                static (o, v) => o.Description = v);

        public string Description
        {
            get => _description;
            set => SetAndRaise(DescriptionProperty, ref _description, value);
        }

        public PopupRunningStatus()
        {
            InitializeComponent();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            _isUnloading = false;
            if (IsVisible)
                StartAnim();
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

        private string _description = string.Empty;
        private bool _isUnloading = false;
    }
}
