using System;
using System.Collections.Generic;
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

        public void Subscribe(Models.ICommandLogReceiver receiver)
        {
            _receivers.Add(receiver);
        }

        public void Unsubscribe(Models.ICommandLogReceiver receiver)
        {
            _receivers.Remove(receiver);
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

                foreach (var receiver in _receivers)
                    receiver.OnReceiveCommandLog(newline);
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
            _receivers.Clear();
            _builder = null;

            OnPropertyChanged(nameof(IsComplete));
        }

        private string _content = string.Empty;
        private StringBuilder _builder = new StringBuilder();
        private List<Models.ICommandLogReceiver> _receivers = new List<Models.ICommandLogReceiver>();
    }
}
