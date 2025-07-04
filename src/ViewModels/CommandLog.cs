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
        }

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
        }

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
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Invoke(() => AppendLine(line));
            }
            else
            {
                var newline = line ?? string.Empty;
                _builder.AppendLine(newline);
                _onNewLineReceived?.Invoke(newline);
            }
        }

        public void Complete()
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Invoke(Complete);
                return;
            }

            IsComplete = true;
            EndTime = DateTime.Now;

            _content = _builder.ToString();
            _builder.Clear();
            _builder = null;

            OnPropertyChanged(nameof(IsComplete));

            if (_onNewLineReceived != null)
            {
                var dumpHandlers = _onNewLineReceived.GetInvocationList();
                foreach (var d in dumpHandlers)
                    _onNewLineReceived -= (Action<string>)d;
            }
        }

        private string _content = string.Empty;
        private StringBuilder _builder = new StringBuilder();
        private event Action<string> _onNewLineReceived;
    }
}
