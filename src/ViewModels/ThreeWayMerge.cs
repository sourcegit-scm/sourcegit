using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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

        // Aligned diff data for all three panels
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

        public List<Models.TextDiffLine> ResultDiffLines
        {
            get => _resultDiffLines;
            private set => SetProperty(ref _resultDiffLines, value);
        }

        public int DiffMaxLineNumber
        {
            get => _diffMaxLineNumber;
            private set => SetProperty(ref _diffMaxLineNumber, value);
        }

        public string ResultContent
        {
            get => _resultContent;
            private set
            {
                if (SetProperty(ref _resultContent, value))
                {
                    IsModified = true;
                    UpdateAlignedLines();
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

        public int CurrentConflictStartLine
        {
            get => _currentConflictStartLine;
            private set => SetProperty(ref _currentConflictStartLine, value);
        }

        public int CurrentConflictEndLine
        {
            get => _currentConflictEndLine;
            private set => SetProperty(ref _currentConflictEndLine, value);
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

                // Read working copy with conflict markers
                var workingCopyPath = Path.Combine(repoPath, _filePath);
                var workingCopyContent = string.Empty;
                if (File.Exists(workingCopyPath))
                {
                    workingCopyContent = await File.ReadAllTextAsync(workingCopyPath).ConfigureAwait(false);
                }

                Dispatcher.UIThread.Post(() =>
                {
                    _resultContent = workingCopyContent;
                    _originalContent = workingCopyContent;
                    IsModified = false;
                    UpdateAlignedLines();
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

        private void UpdateAlignedLines()
        {
            var oursLines = new List<Models.TextDiffLine>();
            var theirsLines = new List<Models.TextDiffLine>();
            var resultLines = new List<Models.TextDiffLine>();

            if (string.IsNullOrEmpty(_resultContent))
            {
                OursDiffLines = oursLines;
                TheirsDiffLines = theirsLines;
                ResultDiffLines = resultLines;
                DiffMaxLineNumber = 0;
                return;
            }

            var lines = _resultContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            int oursLineNumber = 1;
            int theirsLineNumber = 1;
            int resultLineNumber = 1;

            int i = 0;
            while (i < lines.Length)
            {
                var line = lines[i];

                if (line.StartsWith("<<<<<<<", StringComparison.Ordinal))
                {
                    // Start of conflict - add marker line
                    oursLines.Add(new Models.TextDiffLine()); // Empty placeholder
                    theirsLines.Add(new Models.TextDiffLine()); // Empty placeholder
                    resultLines.Add(new Models.TextDiffLine(Models.TextDiffLineType.Indicator, line, 0, resultLineNumber++));
                    i++;

                    // Collect mine content
                    var mineContent = new List<string>();
                    while (i < lines.Length && !lines[i].StartsWith("|||||||", StringComparison.Ordinal) && !lines[i].StartsWith("=======", StringComparison.Ordinal))
                    {
                        mineContent.Add(lines[i]);
                        i++;
                    }

                    // Skip diff3 base section if present
                    if (i < lines.Length && lines[i].StartsWith("|||||||", StringComparison.Ordinal))
                    {
                        // Add base marker as empty in all panels (we're skipping base display)
                        i++;
                        while (i < lines.Length && !lines[i].StartsWith("=======", StringComparison.Ordinal))
                        {
                            i++;
                        }
                    }

                    // Skip separator
                    if (i < lines.Length && lines[i].StartsWith("=======", StringComparison.Ordinal))
                    {
                        // Add separator line
                        oursLines.Add(new Models.TextDiffLine()); // Empty placeholder
                        theirsLines.Add(new Models.TextDiffLine()); // Empty placeholder
                        resultLines.Add(new Models.TextDiffLine(Models.TextDiffLineType.Indicator, lines[i], 0, resultLineNumber++));
                        i++;
                    }

                    // Collect theirs content
                    var theirsContent = new List<string>();
                    while (i < lines.Length && !lines[i].StartsWith(">>>>>>>", StringComparison.Ordinal))
                    {
                        theirsContent.Add(lines[i]);
                        i++;
                    }

                    // Add mine content lines (aligned)
                    foreach (var mineLine in mineContent)
                    {
                        oursLines.Add(new Models.TextDiffLine(Models.TextDiffLineType.Deleted, mineLine, oursLineNumber++, 0));
                        theirsLines.Add(new Models.TextDiffLine()); // Empty placeholder
                        resultLines.Add(new Models.TextDiffLine(Models.TextDiffLineType.Deleted, mineLine, 0, resultLineNumber++));
                    }

                    // Add theirs content lines (aligned)
                    foreach (var theirsLine in theirsContent)
                    {
                        oursLines.Add(new Models.TextDiffLine()); // Empty placeholder
                        theirsLines.Add(new Models.TextDiffLine(Models.TextDiffLineType.Added, theirsLine, 0, theirsLineNumber++));
                        resultLines.Add(new Models.TextDiffLine(Models.TextDiffLineType.Added, theirsLine, 0, resultLineNumber++));
                    }

                    // Add end marker
                    if (i < lines.Length && lines[i].StartsWith(">>>>>>>", StringComparison.Ordinal))
                    {
                        oursLines.Add(new Models.TextDiffLine()); // Empty placeholder
                        theirsLines.Add(new Models.TextDiffLine()); // Empty placeholder
                        resultLines.Add(new Models.TextDiffLine(Models.TextDiffLineType.Indicator, lines[i], 0, resultLineNumber++));
                        i++;
                    }
                }
                else
                {
                    // Normal line - same in all three panels
                    oursLines.Add(new Models.TextDiffLine(Models.TextDiffLineType.Normal, line, oursLineNumber++, oursLineNumber - 1));
                    theirsLines.Add(new Models.TextDiffLine(Models.TextDiffLineType.Normal, line, theirsLineNumber++, theirsLineNumber - 1));
                    resultLines.Add(new Models.TextDiffLine(Models.TextDiffLineType.Normal, line, resultLineNumber, resultLineNumber++));
                    i++;
                }
            }

            var maxLineNumber = Math.Max(Math.Max(oursLineNumber, theirsLineNumber), resultLineNumber);

            OursDiffLines = oursLines;
            TheirsDiffLines = theirsLines;
            ResultDiffLines = resultLines;
            DiffMaxLineNumber = maxLineNumber;
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
        }

        public void AcceptCurrentOurs()
        {
            if (string.IsNullOrEmpty(_resultContent) || _currentConflictIndex < 0)
                return;

            var result = ResolveConflictAtIndex(_resultContent, _currentConflictIndex, useOurs: true);
            ResultContent = result;
        }

        public void AcceptCurrentTheirs()
        {
            if (string.IsNullOrEmpty(_resultContent) || _currentConflictIndex < 0)
                return;

            var result = ResolveConflictAtIndex(_resultContent, _currentConflictIndex, useOurs: false);
            ResultContent = result;
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
                CurrentConflictStartLine = -1;
                CurrentConflictEndLine = -1;
            }
            else if (CurrentConflictIndex >= _totalConflicts)
            {
                CurrentConflictIndex = _totalConflicts - 1;
                UpdateCurrentConflictLine();
            }
            else
            {
                // Update line info for current conflict
                UpdateCurrentConflictLine();
            }
        }

        private void UpdateCurrentConflictLine()
        {
            if (string.IsNullOrEmpty(_resultContent) || _currentConflictIndex < 0)
            {
                CurrentConflictLine = -1;
                CurrentConflictStartLine = -1;
                CurrentConflictEndLine = -1;
                return;
            }

            // Find the conflict region in the aligned ResultDiffLines
            if (_resultDiffLines != null)
            {
                int conflictCount = 0;
                int startLine = -1;
                int endLine = -1;

                for (int i = 0; i < _resultDiffLines.Count; i++)
                {
                    var diffLine = _resultDiffLines[i];
                    if (diffLine.Type == Models.TextDiffLineType.Indicator &&
                        diffLine.Content.StartsWith("<<<<<<<", StringComparison.Ordinal))
                    {
                        if (conflictCount == _currentConflictIndex)
                        {
                            startLine = i;
                        }
                        conflictCount++;
                    }
                    else if (diffLine.Type == Models.TextDiffLineType.Indicator &&
                             diffLine.Content.StartsWith(">>>>>>>", StringComparison.Ordinal))
                    {
                        if (startLine >= 0 && endLine < 0)
                        {
                            endLine = i;
                            break;
                        }
                    }
                }

                CurrentConflictLine = startLine;
                CurrentConflictStartLine = startLine;
                CurrentConflictEndLine = endLine;
            }
            else
            {
                CurrentConflictLine = -1;
                CurrentConflictStartLine = -1;
                CurrentConflictEndLine = -1;
            }
        }

        private string ResolveConflictAtIndex(string content, int conflictIndex, bool useOurs)
        {
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var resultLines = new List<string>();

            int i = 0;
            int currentConflict = 0;

            while (i < lines.Length)
            {
                var line = lines[i];

                if (line.StartsWith("<<<<<<<", StringComparison.Ordinal))
                {
                    bool isTargetConflict = (currentConflict == conflictIndex);

                    // Collect sections
                    var oursLines = new List<string>();
                    var theirsLines = new List<string>();
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

        private string ResolveAllConflicts(string content, List<Models.ConflictMarkerInfo> markers, bool useOurs)
        {
            if (markers.Count == 0)
                return content;

            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var resultLines = new List<string>();

            int i = 0;
            while (i < lines.Length)
            {
                var line = lines[i];

                if (line.StartsWith("<<<<<<<", StringComparison.Ordinal))
                {
                    // Start of conflict - collect sections
                    var oursLines = new List<string>();
                    var theirsLines = new List<string>();
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
        private string _resultContent = string.Empty;
        private string _originalContent = string.Empty;
        private bool _isLoading = false;
        private bool _isModified = false;
        private int _unresolvedConflictCount = 0;
        private int _currentConflictIndex = -1;
        private int _currentConflictLine = -1;
        private int _currentConflictStartLine = -1;
        private int _currentConflictEndLine = -1;
        private int _totalConflicts = 0;
        private List<Models.TextDiffLine> _oursDiffLines = [];
        private List<Models.TextDiffLine> _theirsDiffLines = [];
        private List<Models.TextDiffLine> _resultDiffLines = [];
        private int _diffMaxLineNumber = 0;
    }
}
