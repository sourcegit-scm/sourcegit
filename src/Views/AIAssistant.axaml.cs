using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Threading;

namespace SourceGit.Views
{
    public partial class AIAssistant : ChromelessWindow
    {
        public AIAssistant()
        {
            _cancel = new CancellationTokenSource();
            InitializeComponent();
        }

        public AIAssistant(Models.OpenAIService service, string repo, List<Models.Change> changes, Action<string> onDone)
        {
            _service = service;
            _repo = repo;
            _changes = changes;
            _onDone = onDone;
            _cancel = new CancellationTokenSource();

            InitializeComponent();
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            if (string.IsNullOrEmpty(_repo))
                return;

            Task.Run(() =>
            {
                var message = new Commands.GenerateCommitMessage(_service, _repo, _changes, _cancel.Token, SetDescription).Result();
                if (_cancel.IsCancellationRequested)
                    return;

                Dispatcher.UIThread.Invoke(() =>
                {
                    _onDone?.Invoke(message);
                    Close();
                });
            }, _cancel.Token);
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);
            _cancel.Cancel();
        }

        private void SetDescription(string message)
        {
            Dispatcher.UIThread.Invoke(() => ProgressMessage.Text = message);
        }

        private Models.OpenAIService _service;
        private string _repo;
        private List<Models.Change> _changes;
        private Action<string> _onDone;
        private CancellationTokenSource _cancel;
    }
}
