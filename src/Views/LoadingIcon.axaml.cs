using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class LoadingIcon : UserControl
    {
        public LoadingIcon()
        {
            IsHitTestVisible = false;
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
            Content = new Path() { Classes = { "rotating" } };
        }

        private void StopAnim()
        {
            if (Content is Path path)
                path.Classes.Clear();

            Content = null;
        }
    }
}
