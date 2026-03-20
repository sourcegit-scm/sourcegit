using System.Collections.Generic;
using Avalonia.Controls;

namespace SourceGit.Views
{
    public partial class CommitRelationTracking : UserControl
    {
        public CommitRelationTracking()
        {
            InitializeComponent();
        }

        public void SetData(List<Models.Decorator> data)
        {
            Container.ItemsSource = data;
        }
    }
}
