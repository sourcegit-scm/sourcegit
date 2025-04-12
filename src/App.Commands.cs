using System;
using System.Windows.Input;
using Avalonia.Controls;

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
#if DISABLE_UPDATE_DETECTION
                return false;
#else
                return true;
#endif
            }
        }

        public static readonly Command OpenPreferencesCommand = new(_ => OpenDialog(new Views.Preferences()));
        public static readonly Command OpenHotkeysCommand = new(_ => OpenDialog(new Views.Hotkeys()));
        public static readonly Command OpenAppDataDirCommand = new(_ => Native.OS.OpenInFileManager(Native.OS.DataDir));
        public static readonly Command OpenAboutCommand = new(_ => OpenDialog(new Views.About()));
        public static readonly Command CheckForUpdateCommand = new(_ => (Current as App)?.Check4Update(true));
        public static readonly Command QuitCommand = new(_ => Quit(0));
        public static readonly Command CopyTextBlockCommand = new(p =>
        {
            if (p is not TextBlock textBlock)
                return;

            if (textBlock.Inlines is { Count: > 0 } inlines)
                CopyText(inlines.Text);
            else if (!string.IsNullOrEmpty(textBlock.Text))
                CopyText(textBlock.Text);
        });
    }
}
