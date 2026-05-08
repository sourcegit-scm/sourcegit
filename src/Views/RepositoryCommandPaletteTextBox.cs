using System;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public class RepositoryCommandPaletteTextBox : TextBox
    {
        protected override Type StyleKeyOverride => typeof(TextBox);

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            Focus(NavigationMethod.Directional);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Back && string.IsNullOrEmpty(Text))
            {
                var launcher = App.GetLauncher();
                if (launcher is { ActivePage: { Data: ViewModels.Repository repo } })
                {
                    launcher.CommandPalette = new ViewModels.RepositoryCommandPalette(repo);
                    e.Handled = true;
                    return;
                }
            }

            base.OnKeyDown(e);
        }
    }
}
