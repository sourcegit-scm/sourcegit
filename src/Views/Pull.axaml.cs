using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;

namespace SourceGit.Views
{
    public partial class Pull : UserControl
    {
        public Pull()
        {
            InitializeComponent();
            TextSearch.SetTextBinding(RemoteBranchesComboBox, new Binding("Name"));
        }
    }
}
