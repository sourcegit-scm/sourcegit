using Avalonia.Controls;

namespace SourceGit.Views
{
    public partial class CheckoutCommit : UserControl
    {
        public bool HasLocalChanges;
        public CheckoutCommit()
        {
            InitializeComponent();
        }
    }
}
