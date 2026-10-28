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
        public List<string> AvailableModels
        {
            get => _service.CachedAvailableModels;
        }

        public string SelectedModel
        {
            get => _selectedModel;
            set
            {
                if (SetProperty(ref _selectedModel, value))
                {
                    _service.Model = value;

                    if (!_isFetchingModels)
                    {
                        var _ = GenAsync();
                    }
                }
            }
        }

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

        public string Response
        {
            get => _response;
            private set => SetProperty(ref _response, value);
        }

        public AIAssistant(Repository repo, AI.Service service, List<Models.Change> changes)
        {
            _repo = repo;
            _service = service;
            _selectedModel = service.Model;
            _cancel = new CancellationTokenSource();

            var builder = new StringBuilder();
            foreach (var c in changes)
                SerializeChange(c, builder);
            _changeList = builder.ToString();

            if (changes.Count > 0 && changes[0].DataForAmend is { } amend)
                _amendParent = amend.ParentSHA;
        }

        public async Task GenAsync()
        {
            if (_cancel is { IsCancellationRequested: false })
                _cancel.Cancel();
            _cancel = new CancellationTokenSource();

            var builder = new StringBuilder();
            var responseBuilder = new StringBuilder();
            var foundResponse = false;
            var currentBranchName = _repo.CurrentBranch?.Name ?? "main";

            Text = string.Empty;
            Response = string.Empty;
            IsGenerating = true;

            try
            {
                if (_service.AutoFetchAvailableModels)
                {
                    if (_service.CachedAvailableModels.Count == 0)
                    {
                        _isFetchingModels = true;
                        builder.AppendLine("Fetching available models...");
                        Text = builder.ToString();

                        var models = await _service.GetOpenAIClient().GetOpenAIModelClient().GetModelsAsync();
                        var availableModels = new List<string>();
                        foreach (var m in models.Value)
                            availableModels.Add(m.Id);

                        if (availableModels.Count == 0)
                            throw new Exception("No available models found. Please check your OpenAI API key and network connection.");

                        var lastUsedModel = _selectedModel;
                        _service.CachedAvailableModels = availableModels;
                        OnPropertyChanged(nameof(AvailableModels));

                        if (!availableModels.Contains(lastUsedModel))
                        {
                            SelectedModel = availableModels[0];
                            builder.AppendLine($"The model({lastUsedModel}) last used is not available. Auto change to the first available model.");
                            Text = builder.ToString();
                        }
                        else
                        {
                            SelectedModel = null; // Force update views.
                            SelectedModel = lastUsedModel;
                        }
                        _isFetchingModels = false;
                    }
                }
                else if (string.IsNullOrEmpty(_selectedModel))
                {
                    throw new Exception("No model is selected and auto-fetching is disabled. Please select a model or enable auto-fetching.");
                }

                builder.AppendLine("Asking AI to generate commit message...").AppendLine();
                Text = builder.ToString();

                await new AI.Agent(_service).GenerateCommitMessageAsync(_repo.FullPath, currentBranchName, _changeList, _amendParent, message =>
                {
                    builder.AppendLine(message);

                    if (foundResponse)
                    {
                        if (message.Equals("# Token Usage", StringComparison.Ordinal))
                            foundResponse = false;
                        else
                            responseBuilder.AppendLine(message);
                    }
                    else if (message.Equals("# Assistant", StringComparison.Ordinal))
                    {
                        foundResponse = true;
                    }

                    Dispatcher.UIThread.Post(() => Text = builder.ToString());
                }, _cancel.Token);

                Response = responseBuilder.ToString().Trim();
            }
            catch (OperationCanceledException)
            {
                // Do nothing and leave `IsGenerating` to current (may already changed), so that the UI can update accordingly.
                return;
            }
            catch (Exception e)
            {
                builder
                    .AppendLine()
                    .AppendLine("[ERROR]")
                    .Append(e.Message);

                Text = builder.ToString();
                Response = string.Empty;
            }

            IsGenerating = false;
        }

        public void Use(string text)
        {
            _repo.SetCommitMessage(text);
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

        private readonly Repository _repo = null;
        private readonly AI.Service _service = null;
        private readonly string _changeList = null;
        private readonly string _amendParent = null;
        private string _selectedModel = string.Empty;
        private bool _isFetchingModels = false;
        private CancellationTokenSource _cancel = null;
        private bool _isGenerating = false;
        private string _text = string.Empty;
        private string _response = string.Empty;
    }
}
