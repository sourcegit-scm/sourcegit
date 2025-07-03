using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class Statistics : ChromelessWindow
    {
        public Statistics()
        {
            InitializeComponent();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            if (DataContext is ViewModels.Statistics vm)
                vm.Load();
        }
    }
}
