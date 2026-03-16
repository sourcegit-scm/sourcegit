using System;

using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace SourceGit.Views
{
    public class WorktreeIcon : Path
    {
        protected override Type StyleKeyOverride => typeof(Path);

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (DataContext is ViewModels.Worktree wt)
            {
                if (wt.IsCurrent)
                    Data = this.FindResource("Icons.CheckCircled") as StreamGeometry;
                else if (wt.IsMain)
                    Data = this.FindResource("Icons.Repositories") as StreamGeometry;
                else
                    Data = this.FindResource("Icons.Worktree") as StreamGeometry;

                return;
            }

            Data = null;
        }
    }
}
