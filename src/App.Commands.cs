using System;
using System.Windows.Input;
using Avalonia.Controls;
using SourceGit.Native;

namespace SourceGit
{
    public partial class App
    {
        public class Command : ICommand
        {
            public event EventHandler CanExecuteChanged
            {
                add { }
                remove { }
            }

            public Command(Action<object> action)
            {
                _action = action;
            }

            public bool CanExecute(object parameter) => _action != null;
            public void Execute(object parameter) => _action?.Invoke(parameter);

            private Action<object> _action = null;
        }

        public static bool IsCheckForUpdateCommandVisible
        {
            get
            {
                return OS.ShouldCheckForUpdates();
            }
        }

        public static readonly Command OpenPreferencesCommand = new Command(async _ => await ShowDialog(new Views.Preferences()));
        public static readonly Command OpenHotkeysCommand = new Command(async _ => await ShowDialog(new Views.Hotkeys()));
        public static readonly Command OpenAppDataDirCommand = new Command(_ => Native.OS.OpenInFileManager(Native.OS.DataDir));
        public static readonly Command OpenAboutCommand = new Command(async _ => await ShowDialog(new Views.About()));
        public static readonly Command CheckForUpdateCommand = new Command(_ => (Current as App)?.Check4Update(true));
        public static readonly Command QuitCommand = new Command(_ => Quit(0));
        public static readonly Command CopyTextBlockCommand = new Command(async p =>
        {
            if (p is not TextBlock textBlock)
                return;

            if (textBlock.Inlines is { Count: > 0 } inlines)
                await CopyTextAsync(inlines.Text);
            else if (!string.IsNullOrEmpty(textBlock.Text))
                await CopyTextAsync(textBlock.Text);
        });
    }
}
