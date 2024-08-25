using System;
using System.Windows.Input;
using Avalonia.Controls;

namespace SourceGit
{
    public partial class App
    {
        public class SimpleCommand : ICommand
        {
            public event EventHandler CanExecuteChanged
            {
                add { }
                remove { }
            }

            public SimpleCommand(Action action)
            {
                _action = action;
            }

            public bool CanExecute(object parameter) => _action != null;
            public void Execute(object parameter) => _action?.Invoke();

            private Action _action = null;
        }

        public class ParameterCommand : ICommand
        {
            public event EventHandler CanExecuteChanged
            {
                add { }
                remove { }
            }

            public ParameterCommand(Action<object> action)
            {
                _action = action;
            }

            public bool CanExecute(object parameter) => _action != null;
            public void Execute(object parameter) => _action?.Invoke(parameter);

            private Action<object> _action = null;
        }
        
        public static readonly SimpleCommand OpenPreferenceCommand = new SimpleCommand(() => OpenDialog(new Views.Preference()));
        public static readonly SimpleCommand OpenHotkeysCommand = new SimpleCommand(() => OpenDialog(new Views.Hotkeys()));
        public static readonly SimpleCommand OpenAppDataDirCommand = new SimpleCommand(() => Native.OS.OpenInFileManager(Native.OS.DataDir));
        public static readonly SimpleCommand OpenAboutCommand = new SimpleCommand(() => OpenDialog(new Views.About()));
        public static readonly SimpleCommand CheckForUpdateCommand = new SimpleCommand(() => Check4Update(true));
        public static readonly SimpleCommand QuitCommand = new SimpleCommand(() => Quit(0));

        public static readonly ParameterCommand CopyTextCommand = new ParameterCommand(param =>
        {
            if (param is TextBlock textBlock)
            {
                if (textBlock.Inlines is { Count: > 0 } inlines)
                    CopyText(inlines.Text);
                else
                    CopyText(textBlock.Text);
            }
        });
    }
}
