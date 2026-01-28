using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class AIAssistant : ObservableObject
    {
        public bool IsGenerating
        {
            get => _isGenerating;
            private set => SetProperty(ref _isGenerating, value);
        }

        public string Text
        {
            get => _text;
            private set => SetProperty(ref _text, value);
        }

        public AIAssistant(Repository repo, Models.OpenAIService service, List<Models.Change> changes)
        {
            _repo = repo;
            _service = service;
            _changes = changes;
            _cancel = new CancellationTokenSource();

            Gen();
        }

        public void Regen()
        {
            if (_cancel is { IsCancellationRequested: false })
                _cancel.Cancel();

            Gen();
        }

        public void Apply()
        {
            _repo.SetCommitMessage(Text);
        }

        public void Cancel()
        {
            _cancel?.Cancel();
        }

        private void Gen()
        {
            Text = string.Empty;
            IsGenerating = true;

            _cancel = new CancellationTokenSource();
            Task.Run(async () =>
            {
                await new Commands.GenerateCommitMessage(_service, _repo.FullPath, _changes, _cancel.Token, message =>
                {
                    Dispatcher.UIThread.Post(() => Text = message);
                }).ExecAsync().ConfigureAwait(false);

                Dispatcher.UIThread.Post(() => IsGenerating = false);
            }, _cancel.Token);
        }

        private readonly Repository _repo = null;
        private Models.OpenAIService _service = null;
        private List<Models.Change> _changes = null;
        private CancellationTokenSource _cancel = null;
        private bool _isGenerating = false;
        private string _text = string.Empty;
    }
}
