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

        public static readonly Command OpenPreferenceCommand = new Command(_ => OpenDialog(new Views.Preference()));
        public static readonly Command OpenHotkeysCommand = new Command(_ => OpenDialog(new Views.Hotkeys()));
        public static readonly Command OpenAppDataDirCommand = new Command(_ => Native.OS.OpenInFileManager(Native.OS.DataDir));
        public static readonly Command OpenAboutCommand = new Command(_ => OpenDialog(new Views.About()));
        public static readonly Command CheckForUpdateCommand = new Command(_ => Check4Update(true));
        public static readonly Command QuitCommand = new Command(_ => Quit(0));
        public static readonly Command CopyTextBlockCommand = new Command(p => CopyTextBlock(p as TextBlock));
    }
}
