using System.Threading.Tasks;
using Avalonia.Controls;

namespace DevBoard.Views
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
