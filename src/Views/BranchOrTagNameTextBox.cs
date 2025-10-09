using System;
using System.Text;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public class BranchOrTagNameTextBox : TextBox
    {
        protected override Type StyleKeyOverride => typeof(TextBox);

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            PastingFromClipboard += OnPastingFromClipboard;
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            PastingFromClipboard -= OnPastingFromClipboard;
            base.OnUnloaded(e);
        }

        protected override void OnTextInput(TextInputEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text))
                return;

            var builder = new StringBuilder(e.Text.Length);
            var chars = e.Text.ToCharArray();
            foreach (var ch in chars)
            {
                if (char.IsWhiteSpace(ch))
                    builder.Append('-');
                else
                    builder.Append(ch);
            }

            e.Text = builder.ToString();
            base.OnTextInput(e);
        }

        private async void OnPastingFromClipboard(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            try
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                {
                    var text = await clipboard.TryGetTextAsync();
                    if (!string.IsNullOrEmpty(text))
                        OnTextInput(new TextInputEventArgs() { Text = text });
                }
            }
            catch
            {
                // Ignore exceptions
            }
        }
    }
}
