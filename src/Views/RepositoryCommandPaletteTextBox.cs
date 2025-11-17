using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public class RepositoryCommandPaletteTextBox : TextBox
    {
        protected override Type StyleKeyOverride => typeof(TextBox);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Back && string.IsNullOrEmpty(Text))
            {
                var launcherView = this.FindAncestorOfType<Launcher>(false);
                if (launcherView is { DataContext: ViewModels.Launcher launcher } &&
                    launcher.ActivePage is { Data: ViewModels.Repository repo })
                {
                    launcher.OpenCommandPalette(new ViewModels.RepositoryCommandPalette(launcher, repo));
                    e.Handled = true;
                    return;
                }
            }

            base.OnKeyDown(e);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            Focus();
        }
    }
}
