using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class AIDiffAnalysis : ChromelessWindow
    {
        public AIDiffAnalysis()
        {
            CloseOnESC = true;
            InitializeComponent();
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);
            (DataContext as ViewModels.AIDiffAnalysis)?.Cancel();
        }

        private async void OnCopyClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.AIDiffAnalysis vm && !string.IsNullOrEmpty(vm.Result))
                await this.CopyTextAsync(vm.Result);

            e.Handled = true;
        }

        private async void OnRetryClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.AIDiffAnalysis vm)
            {
                vm.Retry();
                // Re-trigger the current analysis based on context
                // The caller must handle retry logic by setting a new DataContext
            }

            e.Handled = true;
        }

        private async void OnModelChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_ready || DataContext is not ViewModels.AIDiffAnalysis vm)
                return;
            if (vm.IsAnalyzing)
                return;

            e.Handled = true;
            await vm.ReanalyzeAsync();
        }

        public void MarkReady()
        {
            _ready = true;
        }

        private bool _ready = false;
    }
}
