using System;

using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace SourceGit.Views
{
    public class RepositoryCommandPaletteIcon : Path
    {
        protected override Type StyleKeyOverride => typeof(Path);

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (DataContext is ViewModels.RepositoryCommandPaletteCmd cmd && !string.IsNullOrEmpty(cmd.Icon))
            {
                var geo = this.FindResource($"Icons.{cmd.Icon}") as StreamGeometry;
                if (geo != null)
                {
                    Data = geo;
                    return;
                }
            }

            Data = this.FindResource("Icons.Command") as StreamGeometry;
        }
    }
}
