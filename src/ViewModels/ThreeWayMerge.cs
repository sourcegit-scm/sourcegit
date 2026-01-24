using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class ThreeWayMerge : ObservableObject
    {
        public string Title => App.Text("ThreeWayMerge.Title", _filePath);

        public string FilePath
        {
            get => _filePath;
        }

        public string BaseContent
        {
            get => _baseContent;
            private set => SetProperty(ref _baseContent, value);
        }

        public string OursContent
        {
            get => _oursContent;
            private set => SetProperty(ref _oursContent, value);
        }

        public string TheirsContent
        {
            get => _theirsContent;
            private set => SetProperty(ref _theirsContent, value);
        }

        // Aligned diff data for side-by-side view
        public List<Models.TextDiffLine> OursDiffLines
        {
            get => _oursDiffLines;
            private set => SetProperty(ref _oursDiffLines, value);
        }

        public List<Models.TextDiffLine> TheirsDiffLines
        {
            get => _theirsDiffLines;
            private set => SetProperty(ref _theirsDiffLines, value);
        }

        public int DiffMaxLineNumber
        {
            get => _diffMaxLineNumber;
            private set => SetProperty(ref _diffMaxLineNumber, value);
        }

        public Vector DiffScrollOffset
        {
            get => _diffScrollOffset;
            set => SetProperty(ref _diffScrollOffset, value);
        }

        public string ResultContent
        {
            get => _resultContent;
            set
            {
                if (SetProperty(ref _resultContent, value))
                {
                    IsModified = true;
                    UpdateConflictInfo();
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public bool IsModified
        {
            get => _isModified;
            private set => SetProperty(ref _isModified, value);
        }

        public int UnresolvedConflictCount
        {
            get => _unresolvedConflictCount;
            private set
            {
                if (SetProperty(ref _unresolvedConflictCount, value))
                {
                    OnPropertyChanged(nameof(HasUnresolvedConflicts));
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(CanSave));
                }
            }
        }

        public bool HasUnresolvedConflicts => _unresolvedConflictCount > 0;

        public string StatusText
        {
            get
            {
                if (_unresolvedConflictCount > 0)
                    return App.Text("ThreeWayMerge.ConflictsRemaining", _unresolvedConflictCount);
                return App.Text("ThreeWayMerge.AllResolved");
            }
        }

        public bool CanSave => !HasUnresolvedConflicts && IsModified;

        public int CurrentConflictIndex
        {
            get => _currentConflictIndex;
            private set
            {
                if (SetProperty(ref _currentConflictIndex, value))
                {
                    OnPropertyChanged(nameof(HasPrevConflict));
                    OnPropertyChanged(nameof(HasNextConflict));
                }
            }
        }

        public bool HasPrevConflict => _currentConflictIndex > 0;
        public bool HasNextConflict => _currentConflictIndex < _totalConflicts - 1;

        public int CurrentConflictLine
        {
            get => _currentConflictLine;
            private set => SetProperty(ref _currentConflictLine, value);
        }

        public ThreeWayMerge(Repository repo, string filePath)
        {
            _repo = repo;
            _filePath = filePath;
        }

        public async Task LoadAsync()
        {
            IsLoading = true;

            try
            {
                var repoPath = _repo.FullPath;

                // Fetch all three versions and diff in parallel
                var baseTask = Commands.QueryConflictContent.GetBaseContentAsync(repoPath, _filePath);
                var oursTask = Commands.QueryConflictContent.GetOursContentAsync(repoPath, _filePath);
                var theirsTask = Commands.QueryConflictContent.GetTheirsContentAsync(repoPath, _filePath);
                var diffTask = new Commands.DiffConflictStages(repoPath, _filePath).ReadAsync();

                await Task.WhenAll(baseTask, oursTask, theirsTask, diffTask).ConfigureAwait(false);

                var baseContent = await baseTask;
                var oursContent = await oursTask;
                var theirsContent = await theirsTask;
                var diffResult = await diffTask;

                // Read working copy with conflict markers
                var workingCopyPath = Path.Combine(repoPath, _filePath);
                var workingCopyContent = string.Empty;
                if (File.Exists(workingCopyPath))
                {
                    workingCopyContent = await File.ReadAllTextAsync(workingCopyPath).ConfigureAwait(false);
                }

                // Create aligned diff lines for side-by-side view
                var oursLines = new List<Models.TextDiffLine>();
                var theirsLines = new List<Models.TextDiffLine>();
                var maxLineNumber = 0;

                if (diffResult?.TextDiff != null)
                {
                    foreach (var line in diffResult.TextDiff.Lines)
                    {
                        switch (line.Type)
                        {
                            case Models.TextDiffLineType.Added:
                                theirsLines.Add(line);
                                break;
                            case Models.TextDiffLineType.Deleted:
                                oursLines.Add(line);
                                break;
                            default:
                                FillEmptyLines(oursLines, theirsLines);
                                oursLines.Add(line);
                                theirsLines.Add(line);
                                break;
                        }
                    }
                    FillEmptyLines(oursLines, theirsLines);
                    maxLineNumber = diffResult.TextDiff.MaxLineNumber;
                }

                Dispatcher.UIThread.Post(() =>
                {
                    BaseContent = baseContent;
                    OursContent = oursContent;
                    TheirsContent = theirsContent;
                    OursDiffLines = oursLines;
                    TheirsDiffLines = theirsLines;
                    DiffMaxLineNumber = maxLineNumber;
                    ResultContent = workingCopyContent;
                    _originalContent = workingCopyContent;
                    IsModified = false;
                    UpdateConflictInfo();
                    IsLoading = false;
                });
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    App.RaiseException(_repo.FullPath, $"Failed to load conflict data: {ex.Message}");
                    IsLoading = false;
                });
            }
        }

        private void FillEmptyLines(List<Models.TextDiffLine> ours, List<Models.TextDiffLine> theirs)
        {
            if (ours.Count < theirs.Count)
            {
                int diff = theirs.Count - ours.Count;
                for (int i = 0; i < diff; i++)
                    ours.Add(new Models.TextDiffLine());
            }
            else if (ours.Count > theirs.Count)
            {
                int diff = ours.Count - theirs.Count;
                for (int i = 0; i < diff; i++)
                    theirs.Add(new Models.TextDiffLine());
            }
        }

        public void AcceptOurs()
        {
            if (string.IsNullOrEmpty(_resultContent))
                return;

            var markers = Commands.QueryConflictContent.GetConflictMarkers(_resultContent);
            if (markers.Count == 0)
                return;

            var result = ResolveAllConflicts(_resultContent, markers, useOurs: true);
            ResultContent = result;
            UpdateConflictInfo();
        }

        public void AcceptTheirs()
        {
            if (string.IsNullOrEmpty(_resultContent))
                return;

            var markers = Commands.QueryConflictContent.GetConflictMarkers(_resultContent);
            if (markers.Count == 0)
                return;

            var result = ResolveAllConflicts(_resultContent, markers, useOurs: false);
            ResultContent = result;
            UpdateConflictInfo();
        }

        public void AcceptCurrentOurs()
        {
            if (string.IsNullOrEmpty(_resultContent) || _currentConflictIndex < 0)
                return;

            var result = ResolveConflictAtIndex(_resultContent, _currentConflictIndex, useOurs: true);
            ResultContent = result;
            UpdateConflictInfo();
        }

        public void AcceptCurrentTheirs()
        {
            if (string.IsNullOrEmpty(_resultContent) || _currentConflictIndex < 0)
                return;

            var result = ResolveConflictAtIndex(_resultContent, _currentConflictIndex, useOurs: false);
            ResultContent = result;
            UpdateConflictInfo();
        }

        public void GotoPrevConflict()
        {
            if (_currentConflictIndex > 0)
            {
                CurrentConflictIndex--;
                UpdateCurrentConflictLine();
            }
        }

        public void GotoNextConflict()
        {
            if (_currentConflictIndex < _totalConflicts - 1)
            {
                CurrentConflictIndex++;
                UpdateCurrentConflictLine();
            }
        }

        public async Task<bool> SaveAndStageAsync()
        {
            if (HasUnresolvedConflicts)
            {
                App.RaiseException(_repo.FullPath, "Cannot save: there are still unresolved conflicts.");
                return false;
            }

            try
            {
                // Write merged content to file
                var fullPath = Path.Combine(_repo.FullPath, _filePath);
                await File.WriteAllTextAsync(fullPath, _resultContent).ConfigureAwait(false);

                // Stage the file
                var pathSpecFile = Path.GetTempFileName();
                await File.WriteAllTextAsync(pathSpecFile, _filePath);
                await new Commands.Add(_repo.FullPath, pathSpecFile).ExecAsync();
                File.Delete(pathSpecFile);

                _repo.MarkWorkingCopyDirtyManually();
                IsModified = false;
                _originalContent = _resultContent;

                return true;
            }
            catch (Exception ex)
            {
                App.RaiseException(_repo.FullPath, $"Failed to save and stage: {ex.Message}");
                return false;
            }
        }

        public bool HasUnsavedChanges()
        {
            return IsModified && _resultContent != _originalContent;
        }

        private void UpdateConflictInfo()
        {
            if (string.IsNullOrEmpty(_resultContent))
            {
                UnresolvedConflictCount = 0;
                _totalConflicts = 0;
                CurrentConflictIndex = -1;
                return;
            }

            var markers = Commands.QueryConflictContent.GetConflictMarkers(_resultContent);
            var conflictStarts = markers.Where(m => m.Type == Models.ConflictMarkerType.Start).ToList();

            _totalConflicts = conflictStarts.Count;
            UnresolvedConflictCount = conflictStarts.Count;

            if (_totalConflicts > 0 && CurrentConflictIndex < 0)
            {
                CurrentConflictIndex = 0;
                UpdateCurrentConflictLine();
            }
            else if (_totalConflicts == 0)
            {
                CurrentConflictIndex = -1;
                CurrentConflictLine = -1;
            }
        }

        private void UpdateCurrentConflictLine()
        {
            if (string.IsNullOrEmpty(_resultContent) || _currentConflictIndex < 0)
            {
                CurrentConflictLine = -1;
                return;
            }

            var markers = Commands.QueryConflictContent.GetConflictMarkers(_resultContent);
            var conflictStarts = markers.Where(m => m.Type == Models.ConflictMarkerType.Start).ToList();

            if (_currentConflictIndex < conflictStarts.Count)
            {
                CurrentConflictLine = conflictStarts[_currentConflictIndex].LineNumber;
            }
        }

        private string ResolveConflictAtIndex(string content, int conflictIndex, bool useOurs)
        {
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var resultLines = new System.Collections.Generic.List<string>();

            int i = 0;
            int currentConflict = 0;

            while (i < lines.Length)
            {
                var line = lines[i];

                if (line.StartsWith("<<<<<<<", StringComparison.Ordinal))
                {
                    bool isTargetConflict = (currentConflict == conflictIndex);

                    // Collect sections
                    var oursLines = new System.Collections.Generic.List<string>();
                    var theirsLines = new System.Collections.Generic.List<string>();
                    var currentSection = oursLines;
                    int conflictStartLine = i;
                    i++;

                    while (i < lines.Length)
                    {
                        line = lines[i];
                        if (line.StartsWith("|||||||", StringComparison.Ordinal))
                        {
                            // diff3 base section - skip to separator
                            i++;
                            while (i < lines.Length && !lines[i].StartsWith("=======", StringComparison.Ordinal))
                                i++;
                            if (i < lines.Length)
                                currentSection = theirsLines;
                        }
                        else if (line.StartsWith("=======", StringComparison.Ordinal))
                        {
                            currentSection = theirsLines;
                        }
                        else if (line.StartsWith(">>>>>>>", StringComparison.Ordinal))
                        {
                            // End of conflict
                            if (isTargetConflict)
                            {
                                // Resolve this conflict
                                if (useOurs)
                                    resultLines.AddRange(oursLines);
                                else
                                    resultLines.AddRange(theirsLines);
                            }
                            else
                            {
                                // Keep conflict markers for non-target conflicts
                                for (int j = conflictStartLine; j <= i; j++)
                                    resultLines.Add(lines[j]);
                            }
                            break;
                        }
                        else
                        {
                            currentSection.Add(line);
                        }
                        i++;
                    }
                    currentConflict++;
                }
                else
                {
                    resultLines.Add(line);
                }
                i++;
            }

            return string.Join(Environment.NewLine, resultLines);
        }

        private string ResolveAllConflicts(string content, System.Collections.Generic.List<Models.ConflictMarkerInfo> markers, bool useOurs)
        {
            if (markers.Count == 0)
                return content;

            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var resultLines = new System.Collections.Generic.List<string>();

            int i = 0;
            while (i < lines.Length)
            {
                var line = lines[i];

                if (line.StartsWith("<<<<<<<", StringComparison.Ordinal))
                {
                    // Start of conflict - collect sections
                    var oursLines = new System.Collections.Generic.List<string>();
                    var theirsLines = new System.Collections.Generic.List<string>();
                    var currentSection = oursLines;
                    i++;

                    while (i < lines.Length)
                    {
                        line = lines[i];
                        if (line.StartsWith("|||||||", StringComparison.Ordinal))
                        {
                            // diff3 base section - skip to separator
                            i++;
                            while (i < lines.Length && !lines[i].StartsWith("=======", StringComparison.Ordinal))
                                i++;
                            if (i < lines.Length)
                                currentSection = theirsLines;
                        }
                        else if (line.StartsWith("=======", StringComparison.Ordinal))
                        {
                            currentSection = theirsLines;
                        }
                        else if (line.StartsWith(">>>>>>>", StringComparison.Ordinal))
                        {
                            // End of conflict - add resolved content
                            if (useOurs)
                                resultLines.AddRange(oursLines);
                            else
                                resultLines.AddRange(theirsLines);
                            break;
                        }
                        else
                        {
                            currentSection.Add(line);
                        }
                        i++;
                    }
                }
                else
                {
                    resultLines.Add(line);
                }
                i++;
            }

            return string.Join(Environment.NewLine, resultLines);
        }

        private readonly Repository _repo;
        private readonly string _filePath;
        private string _baseContent = string.Empty;
        private string _oursContent = string.Empty;
        private string _theirsContent = string.Empty;
        private string _resultContent = string.Empty;
        private string _originalContent = string.Empty;
        private bool _isLoading = false;
        private bool _isModified = false;
        private int _unresolvedConflictCount = 0;
        private int _currentConflictIndex = -1;
        private int _currentConflictLine = -1;
        private int _totalConflicts = 0;
        private List<Models.TextDiffLine> _oursDiffLines = [];
        private List<Models.TextDiffLine> _theirsDiffLines = [];
        private int _diffMaxLineNumber = 0;
        private Vector _diffScrollOffset = Vector.Zero;
    }
}
