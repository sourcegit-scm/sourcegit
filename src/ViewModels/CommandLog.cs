using System;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class CommandLog : ObservableObject, Models.ICommandLog
    {
        public string Name
        {
            get;
            private set;
        } = string.Empty;

        public DateTime StartTime
        {
            get;
        } = DateTime.Now;

        public DateTime EndTime
        {
            get;
            private set;
        } = DateTime.Now;

        public bool IsComplete
        {
            get;
            private set;
        } = false;

        public string Content
        {
            get
            {
                return IsComplete ? _content : _builder.ToString();
            }
        }

        public CommandLog(string name)
        {
            Name = name;
        }

        public void Register(Action<string> handler)
        {
            if (!IsComplete)
                _onNewLineReceived += handler;
        }

        public void AppendLine(string line = null)
        {
            var newline = line ?? string.Empty;

            Dispatcher.UIThread.Invoke(() =>
            {
                _builder.AppendLine(newline);
                _onNewLineReceived?.Invoke(newline);
            });
        }

        public void Complete()
        {
            IsComplete = true;

            Dispatcher.UIThread.Invoke(() =>
            {
                _content = _builder.ToString();
                _builder.Clear();
                _builder = null;

                EndTime = DateTime.Now;

                OnPropertyChanged(nameof(IsComplete));

                if (_onNewLineReceived != null)
                {
                    var dumpHandlers = _onNewLineReceived.GetInvocationList();
                    foreach (var d in dumpHandlers)
                        _onNewLineReceived -= (Action<string>)d;
                }
            });
        }

        private string _content = string.Empty;
        private StringBuilder _builder = new StringBuilder();
        private event Action<string> _onNewLineReceived;
    }

    public static class CommandExtensions
    {
        public static T Use<T>(this T cmd, CommandLog log) where T : Commands.Command
        {
            cmd.Log = log;
            return cmd;
        }
    }
}
