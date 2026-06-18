using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public partial class AIDiffAnalysis : ObservableObject
    {
        public string Title
        {
            get => _title;
            private set => SetProperty(ref _title, value);
        }

        public bool IsAnalyzing
        {
            get => _isAnalyzing;
            private set
            {
                if (SetProperty(ref _isAnalyzing, value))
                    OnPropertyChanged(nameof(IsModelSelectionEnabled));
            }
        }

        public bool IsModelSelectionEnabled => !_isAnalyzing && _service.AvailableModels.Count > 1;

        public string Result
        {
            get => _result;
            private set => SetProperty(ref _result, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value);
        }

        public bool HasError
        {
            get => _hasError;
            private set => SetProperty(ref _hasError, value);
        }

        public AI.AIDiffContextData DiffData
        {
            get => _diffData;
            private set => SetProperty(ref _diffData, value);
        }

        public string DirectionText
        {
            get => _directionText;
            private set => SetProperty(ref _directionText, value);
        }

        public AIDiffAnalysis(Repository repo, AI.Service service)
        {
            _repo = repo;
            _service = service;
            _cancel = new CancellationTokenSource();
            _title = App.Text("AIDiffAnalysis");
        }

        public List<string> AvailableModels
        {
            get => _service.AvailableModels;
        }

        public string CurrentModel
        {
            get => _service.Model;
            set => _service.Model = value;
        }

        public string LoadingText
        {
            get => _loadingText;
            private set => SetProperty(ref _loadingText, value);
        }

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        public async Task AnalyzeWorkingTreeAsync()
        {
            _analyzeType = AnalyzeType.WorkingTree;
            _title = App.Text("AIDiffAnalysis");
            IsAnalyzing = true;
            HasError = false;
            ErrorMessage = string.Empty;
            Result = string.Empty;
            DirectionText = App.Text("AIDiffAnalysis.WorkingTree");
            LoadingText = App.Text("AIDiffAnalysis.Collecting");
            StatusText = string.Empty;

            try
            {
                var builder = new AI.AIDiffContextBuilder();
                _diffData = await Task.Run(() => builder.CollectWorkingTreeAsync(_repo.FullPath));

                if (_diffData.TotalFiles == 0)
                {
                    ErrorMessage = App.Text("AIDiffAnalysis.NoChanges");
                    HasError = true;
                    IsAnalyzing = false;
                    return;
                }

                UpdateStatusFromData();

                var locale = Preferences.Instance.Locale;
                var language = AI.DiffPrompts.GetOutputLanguage(locale);
                var additionalPrompt = _service.AdditionalPrompt;
                var prompt = AI.DiffPrompts.BuildWorkingTreePrompt(_diffData, language, additionalPrompt);

                LoadingText = App.Text("AIDiffAnalysis.Waiting");
                var agent = new AI.DiffAgent();
                var response = await agent.AnalyzeAsync(_service, prompt, _cancel.Token);

                Result = StripPreamble(response);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                ErrorMessage = MapError(e);
                HasError = true;
            }
            finally
            {
                IsAnalyzing = false;
            }
        }

        public async Task AnalyzeCommitRangeAsync(string fromSHA, string toSHA, string fromName, string toName)
        {
            _analyzeType = AnalyzeType.CommitRange;
            _fromSHA = fromSHA;
            _toSHA = toSHA;
            _fromName = fromName;
            _toName = toName;
            _title = App.Text("AIDiffAnalysis");
            IsAnalyzing = true;
            HasError = false;
            ErrorMessage = string.Empty;
            Result = string.Empty;
            LoadingText = App.Text("AIDiffAnalysis.Collecting");
            StatusText = string.Empty;

            try
            {
                var builder = new AI.AIDiffContextBuilder();
                var (resolvedFrom, resolvedTo) = await Task.Run(() => builder.ResolveCommitDirectionAsync(_repo.FullPath, fromSHA, toSHA));

                var fromShort = resolvedFrom.Length > 8 ? resolvedFrom.Substring(0, 8) : resolvedFrom;
                var toShort = resolvedTo.Length > 8 ? resolvedTo.Substring(0, 8) : resolvedTo;
                DirectionText = $"{fromShort} → {toShort}";

                _diffData = await Task.Run(() => builder.CollectCommitRangeAsync(_repo.FullPath, resolvedFrom, resolvedTo));

                if (_diffData.TotalFiles == 0)
                {
                    ErrorMessage = App.Text("AIDiffAnalysis.NoChanges");
                    HasError = true;
                    IsAnalyzing = false;
                    return;
                }

                UpdateStatusFromData();

                var locale = Preferences.Instance.Locale;
                var language = AI.DiffPrompts.GetOutputLanguage(locale);
                var additionalPrompt = _service.AdditionalPrompt;
                var prompt = AI.DiffPrompts.BuildCommitRangePrompt(_diffData, language, additionalPrompt);

                LoadingText = App.Text("AIDiffAnalysis.Waiting");
                var agent = new AI.DiffAgent();
                var response = await agent.AnalyzeAsync(_service, prompt, _cancel.Token);

                Result = StripPreamble(response);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                ErrorMessage = MapError(e);
                HasError = true;
            }
            finally
            {
                IsAnalyzing = false;
            }
        }

        public void Cancel()
        {
            if (_cancel is { IsCancellationRequested: false })
                _cancel.Cancel();
        }

        public void Retry()
        {
            _cancel = new CancellationTokenSource();
        }

        public async Task ReanalyzeAsync()
        {
            Cancel();
            _cancel = new CancellationTokenSource();
            if (_analyzeType == AnalyzeType.CommitRange)
                await AnalyzeCommitRangeAsync(_fromSHA, _toSHA, _fromName, _toName);
            else
                await AnalyzeWorkingTreeAsync();
        }

        private enum AnalyzeType { WorkingTree, CommitRange }

        private void UpdateStatusFromData()
        {
            if (_diffData == null)
                return;

            var parts = new System.Collections.Generic.List<string>();
            if (_diffData.IsTruncated)
                parts.Add(App.Text("AIDiffAnalysis.Truncated"));
            var skipCount = _diffData.SkippedBinaryFiles.Count + _diffData.SkippedLargeFiles.Count;
            if (skipCount > 0)
                parts.Add(string.Format(App.Text("AIDiffAnalysis.SkippedFiles"), skipCount));
            StatusText = parts.Count > 0 ? string.Join(" · ", parts) : string.Empty;
        }

        private static string MapError(Exception e)
        {
            if (e is OperationCanceledException)
                return string.Empty;

            var msg = e.Message ?? string.Empty;
            var lower = msg.ToLowerInvariant();

            if (lower.Contains("content filter"))
                return App.Text("AIDiffAnalysis.ContentFiltered");
            if (lower.Contains("maximum length") || lower.Contains("max tokens") || lower.Contains("too long"))
                return App.Text("AIDiffAnalysis.ResponseLengthExceeded");
            if (lower.Contains("cut off") || lower.Contains("truncat"))
                return App.Text("AIDiffAnalysis.ResponseLengthExceeded");
            if (e is InvalidOperationException && lower.Contains("not configured"))
                return App.Text("AIDiffAnalysis.NoService");

            return App.Text("AIDiffAnalysis.AIFailed");
        }

        private static string StripPreamble(string response)
        {
            if (string.IsNullOrEmpty(response))
                return response;

            var trimmed = response.TrimStart();
            var firstLineEnd = trimmed.IndexOf('\n');
            if (firstLineEnd <= 0 || firstLineEnd > 120)
                return response;

            var firstLine = trimmed.AsSpan(0, firstLineEnd).Trim();
            if (firstLine.IsEmpty)
                return trimmed.Substring(firstLineEnd + 1).TrimStart();

            if (firstLine.Contains("好的", StringComparison.Ordinal) && firstLine.Length < 60)
                return trimmed.Substring(firstLineEnd + 1).TrimStart();
            if (firstLine.StartsWith("以下是", StringComparison.Ordinal) && firstLine.Length < 80)
                return trimmed.Substring(firstLineEnd + 1).TrimStart();
            if ((firstLine.StartsWith("Here is", StringComparison.OrdinalIgnoreCase) ||
                 firstLine.StartsWith("Here's", StringComparison.OrdinalIgnoreCase) ||
                 firstLine.StartsWith("Below is", StringComparison.OrdinalIgnoreCase) ||
                 firstLine.StartsWith("Sure", StringComparison.OrdinalIgnoreCase) ||
                 firstLine.StartsWith("Certainly", StringComparison.OrdinalIgnoreCase)) &&
                firstLine.Length < 80)
                return trimmed.Substring(firstLineEnd + 1).TrimStart();

            return response;
        }

        private readonly Repository _repo;
        private readonly AI.Service _service;
        private CancellationTokenSource _cancel;
        private AnalyzeType _analyzeType = AnalyzeType.WorkingTree;
        private string _fromSHA = string.Empty;
        private string _toSHA = string.Empty;
        private string _fromName = string.Empty;
        private string _toName = string.Empty;
        private string _title = string.Empty;
        private bool _isAnalyzing = false;
        private string _result = string.Empty;
        private string _errorMessage = string.Empty;
        private bool _hasError = false;
        private AI.AIDiffContextData _diffData = null;
        private string _directionText = string.Empty;
        private string _loadingText = string.Empty;
        private string _statusText = string.Empty;
    }
}
