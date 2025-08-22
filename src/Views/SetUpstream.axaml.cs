using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;

namespace SourceGit.Views
{
    public partial class SetUpstream : UserControl
    {
        public SetUpstream()
        {
            InitializeComponent();
            TextSearch.SetTextBinding(RemoteBranchesComboBox, new Binding("Name"));
        }
    }
}
