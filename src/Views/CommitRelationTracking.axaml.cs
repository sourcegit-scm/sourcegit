using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Threading;

namespace SourceGit.Views
{
    public partial class CommitRelationTracking : UserControl
    {
        public CommitRelationTracking()
        {
            InitializeComponent();
        }

        public CommitRelationTracking(ViewModels.CommitDetail detail)
        {
            InitializeComponent();

            LoadingIcon.IsVisible = true;

            Task.Run(() =>
            {
                var containsIn = detail.GetRefsContainsThisCommit();
                Dispatcher.UIThread.Invoke(() =>
                {
                    Container.ItemsSource = containsIn;
                    LoadingIcon.IsVisible = false;
                });
            });
        }
    }
}
