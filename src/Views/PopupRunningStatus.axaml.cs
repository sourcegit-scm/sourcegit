using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;

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

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            if (IsVisible)
                StartAnim();
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            StopAnim();
            base.OnUnloaded(e);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == IsVisibleProperty)
            {
                if (IsVisible)
                    StartAnim();
                else
                    StopAnim();
            }
        }

        private void StartAnim()
        {
            icon.Content = new Path()
            {
                Data = this.FindResource("Icons.Waiting") as StreamGeometry,
                Classes = { "waiting" },
            };
            progressBar.IsIndeterminate = true;
        }

        private void StopAnim()
        {
            if (icon.Content is Path path)
                path.Classes.Clear();
            icon.Content = null;
            progressBar.IsIndeterminate = false;
        }
    }
}
