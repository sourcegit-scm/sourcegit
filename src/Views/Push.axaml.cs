using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;

namespace SourceGit.Views
{
    public partial class Push : UserControl
    {
        public Push()
        {
            InitializeComponent();
            TextSearch.SetTextBinding(LocalBranchesComboBox, new Binding("Name"));
            TextSearch.SetTextBinding(RemoteBranchesComboBox, new Binding("Name"));
        }
    }
}
