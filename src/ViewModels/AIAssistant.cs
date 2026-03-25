using System;
using System.Collections.Generic;
using System.Text;
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

        public AIAssistant(string repo, AI.Service service, List<Models.Change> changes)
        {
            _repo = repo;
            _service = service;
            _cancel = new CancellationTokenSource();

            var builder = new StringBuilder();
            foreach (var c in changes)
                SerializeChange(c, builder);
            _changeList = builder.ToString();
        }

        public async Task GenAsync()
        {
            if (_cancel is { IsCancellationRequested: false })
                _cancel.Cancel();
            _cancel = new CancellationTokenSource();

            var agent = new AI.Agent(_service);
            var builder = new StringBuilder();
            builder.AppendLine("Asking AI to generate commit message...").AppendLine();

            Text = builder.ToString();
            IsGenerating = true;

            try
            {
                await agent.GenerateCommitMessageAsync(_repo, _changeList, message =>
                {
                    builder.AppendLine(message);
                    Dispatcher.UIThread.Post(() => Text = builder.ToString());
                }, _cancel.Token);
            }
            catch (OperationCanceledException)
            {
                // Do nothing
            }
            catch (Exception e)
            {
                App.RaiseException(_repo, e.Message);
            }

            IsGenerating = false;
        }

        public void Cancel()
        {
            _cancel?.Cancel();
        }

        private void SerializeChange(Models.Change c, StringBuilder builder)
        {
            var status = c.Index switch
            {
                Models.ChangeState.Added => "A",
                Models.ChangeState.Modified => "M",
                Models.ChangeState.Deleted => "D",
                Models.ChangeState.TypeChanged => "T",
                Models.ChangeState.Renamed => "R",
                Models.ChangeState.Copied => "C",
                _ => " ",
            };

            builder.Append(status).Append('\t');

            if (c.Index == Models.ChangeState.Renamed || c.Index == Models.ChangeState.Copied)
                builder.Append(c.OriginalPath).Append(" -> ").Append(c.Path).AppendLine();
            else
                builder.Append(c.Path).AppendLine();
        }

        private readonly string _repo = null;
        private readonly AI.Service _service = null;
        private readonly string _changeList = null;
        private CancellationTokenSource _cancel = null;
        private bool _isGenerating = false;
        private string _text = string.Empty;
    }
}
