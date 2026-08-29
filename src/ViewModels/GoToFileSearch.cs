using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Collections;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DevBoard.ViewModels
{
    public sealed class GoToFileSearch : ObservableObject, IDisposable
    {
        public AvaloniaList<GoToFileSearchResult> Results { get; } = [];

        public string Query
        {
            get => _query;
            set
            {
                if (!SetProperty(ref _query, value ?? string.Empty))
                    return;

                _ = RefreshAsync();
            }
        }

        public GoToFileSearchResult SelectedResult
        {
            get => _selectedResult;
            set => SetProperty(ref _selectedResult, value);
        }

        public string WorkingDirectory => _workingDirectory;

        public GoToFileSearch(string workingDirectory, DevSpaces devSpaces)
        {
            _workingDirectory = workingDirectory;
            _devSpaces = devSpaces;
        }

        public async Task RefreshAsync()
        {
            var query = _query.Trim();
            var version = Interlocked.Increment(ref _searchVersion);

            _searchCancellation?.Cancel();
            _searchCancellation?.Dispose();
            _searchCancellation = new CancellationTokenSource();
            var token = _searchCancellation.Token;

            if (query.Length == 0)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Results.Clear();
                    SelectedResult = null;
                });
                return;
            }

            var paths = _devSpaces.Files.GetSearchableFilePaths();
            var pathMatches = paths
                .Select(path => new { Path = path, Rank = GetPathRank(path, query) })
                .Where(x => x.Rank != int.MaxValue)
                .OrderBy(x => x.Rank)
                .ThenBy(x => x.Path, Comparer<string>.Create(Models.NumericSort.Compare))
                .Take(MaxResults)
                .Select(x => new GoToFileSearchResult(x.Path, GoToFileMatchKind.Path, string.Empty, x.Rank))
                .ToList();

            if (version != _searchVersion || token.IsCancellationRequested)
                return;

            await Dispatcher.UIThread.InvokeAsync(() => PublishResults(pathMatches, version));

            if (pathMatches.Count >= MaxResults)
                return;

            var pathMatchSet = new HashSet<string>(pathMatches.Select(x => x.RelativePath), StringComparer.Ordinal);
            List<GoToFileSearchResult> contentMatches;
            try
            {
                contentMatches = await Task.Run(
                    () => SearchContent(paths, pathMatchSet, query, MaxResults - pathMatches.Count, token),
                    token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (version != _searchVersion || token.IsCancellationRequested)
                return;

            var combined = pathMatches.Concat(contentMatches).Take(MaxResults).ToList();
            await Dispatcher.UIThread.InvokeAsync(() => PublishResults(combined, version));
        }

        public bool OpenSelected()
        {
            if (SelectedResult == null)
                return false;

            return _devSpaces.OpenFile(SelectedResult.RelativePath);
        }

        public void MoveSelection(int delta)
        {
            if (Results.Count == 0)
            {
                SelectedResult = null;
                return;
            }

            var index = SelectedResult == null ? -1 : Results.IndexOf(SelectedResult);
            if (index < 0)
                index = delta >= 0 ? 0 : Results.Count - 1;
            else
                index = Math.Clamp(index + delta, 0, Results.Count - 1);

            SelectedResult = Results[index];
        }

        public void Dispose()
        {
            _searchCancellation?.Cancel();
            _searchCancellation?.Dispose();
            _searchCancellation = null;
        }

        private void PublishResults(IReadOnlyList<GoToFileSearchResult> results, int version)
        {
            if (version != _searchVersion)
                return;

            var selectedPath = SelectedResult?.RelativePath;
            Results.Clear();
            Results.AddRange(results);

            if (!string.IsNullOrEmpty(selectedPath))
                SelectedResult = Results.FirstOrDefault(x => x.RelativePath == selectedPath);

            if (SelectedResult == null && Results.Count > 0)
                SelectedResult = Results[0];
        }

        private List<GoToFileSearchResult> SearchContent(
            IReadOnlyList<string> paths,
            HashSet<string> pathMatches,
            string query,
            int remaining,
            CancellationToken token)
        {
            var results = new List<GoToFileSearchResult>();
            foreach (var relativePath in paths)
            {
                token.ThrowIfCancellationRequested();
                if (results.Count >= remaining || pathMatches.Contains(relativePath))
                    continue;

                var absolutePath = Path.Combine(
                    _workingDirectory,
                    relativePath.Replace('/', Path.DirectorySeparatorChar));

                try
                {
                    var info = new FileInfo(absolutePath);
                    if (!info.Exists || info.Length > MaxSearchBytes)
                        continue;

                    using (var stream = File.OpenRead(absolutePath))
                    {
                        var sampleSize = (int)Math.Min(8192, stream.Length);
                        var sample = new byte[sampleSize];
                        var read = stream.Read(sample, 0, sample.Length);
                        var binary = false;
                        for (var i = 0; i < read; i++)
                        {
                            if (sample[i] != 0)
                                continue;

                            binary = true;
                            break;
                        }

                        if (binary)
                            continue;
                    }

                    foreach (var line in File.ReadLines(absolutePath))
                    {
                        token.ThrowIfCancellationRequested();
                        if (!line.Contains(query, StringComparison.OrdinalIgnoreCase))
                            continue;

                        var preview = line.Trim();
                        if (preview.Length > MaxPreviewCharacters)
                            preview = preview[..MaxPreviewCharacters] + "…";

                        results.Add(new GoToFileSearchResult(relativePath, GoToFileMatchKind.Content, preview, ContentRank));
                        break;
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            results.Sort((left, right) => Models.NumericSort.Compare(left.RelativePath, right.RelativePath));
            return results;
        }

        private static int GetPathRank(string path, string query)
        {
            var fileName = Path.GetFileName(path);
            if (fileName.Equals(query, StringComparison.OrdinalIgnoreCase))
                return 0;
            if (fileName.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                return 10;
            if (fileName.Contains(query, StringComparison.OrdinalIgnoreCase))
                return 20;
            if (path.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                return 30;
            if (path.Contains(query, StringComparison.OrdinalIgnoreCase))
                return 40;
            return int.MaxValue;
        }

        private const int MaxResults = 100;
        private const long MaxSearchBytes = 1024 * 1024;
        private const int MaxPreviewCharacters = 240;
        private const int ContentRank = 1000;

        private readonly string _workingDirectory;
        private readonly DevSpaces _devSpaces;
        private string _query = string.Empty;
        private GoToFileSearchResult _selectedResult;
        private CancellationTokenSource _searchCancellation;
        private int _searchVersion;
    }
}
