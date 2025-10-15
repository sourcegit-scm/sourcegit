using System.Threading.Tasks;
using Avalonia.Controls;

namespace SourceGit.Views
{
    public partial class CommitRelationTracking : UserControl
    {
        public CommitRelationTracking()
        {
            InitializeComponent();
        }

        public async Task SetDataAsync(ViewModels.CommitDetail detail)
        {
            LoadingIcon.IsVisible = true;
            var containsIn = await detail.GetRefsContainsThisCommitAsync();
            Container.ItemsSource = containsIn;
            LoadingIcon.IsVisible = false;
        }
    }
}
