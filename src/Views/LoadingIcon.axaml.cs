using Avalonia;
using Avalonia.Controls;
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
            target.Classes.Add("rotating");
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);
            target.Classes.Clear();
        }
    }
}
