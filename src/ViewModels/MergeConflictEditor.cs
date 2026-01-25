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
    // Represents a single conflict region with its original content and panel positions
    public class ConflictRegion
    {
        public int StartLineInOriginal { get; set; }
        public int EndLineInOriginal { get; set; }
        public List<string> OursContent { get; set; } = new();
        public List<string> TheirsContent { get; set; } = new();
        public bool IsResolved { get; set; } = false;

        // Line indices in the built static panels (0-based)
        public int PanelStartLine { get; set; } = -1;
        public int PanelEndLine { get; set; } = -1;

        // Content chosen when resolved (null = unresolved, empty list = deleted)
        public List<string> ResolvedContent { get; set; } = null;
    }

    public class MergeConflictEditor : ObservableObject
    {
        public string Title => App.Text("MergeConflictEditor.Title", _filePath);

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
                    BuildAlignedResultPanel();
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
                    return App.Text("MergeConflictEditor.ConflictsRemaining", _unresolvedConflictCount);
                return App.Text("MergeConflictEditor.AllResolved");
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

        // All conflict ranges (for fading non-current conflicts)
        public List<(int Start, int End)> AllConflictRanges
        {
            get => _allConflictRanges;
            private set => SetProperty(ref _allConflictRanges, value);
        }

        // Shared scroll offset for synchronized scrolling
        public Vector ScrollOffset
        {
            get => _scrollOffset;
            set => SetProperty(ref _scrollOffset, value);
        }

        public MergeConflictEditor(Repository repo, string filePath)
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

                    // Parse and store original conflict regions
                    ParseOriginalConflicts(workingCopyContent);

                    // Build static MINE/THEIRS panels (these won't change)
                    BuildStaticPanels();

                    // Build result panel aligned with MINE/THEIRS (this changes when conflicts are resolved)
                    BuildAlignedResultPanel();
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

        private void ParseOriginalConflicts(string content)
        {
            _conflictRegions.Clear();

            if (string.IsNullOrEmpty(content))
                return;

            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            int i = 0;

            while (i < lines.Length)
            {
                var line = lines[i];

                if (line.StartsWith("<<<<<<<", StringComparison.Ordinal))
                {
                    var region = new ConflictRegion { StartLineInOriginal = i };
                    i++;

                    // Collect ours content
                    while (i < lines.Length &&
                           !lines[i].StartsWith("|||||||", StringComparison.Ordinal) &&
                           !lines[i].StartsWith("=======", StringComparison.Ordinal))
                    {
                        region.OursContent.Add(lines[i]);
                        i++;
                    }

                    // Skip diff3 base section if present
                    if (i < lines.Length && lines[i].StartsWith("|||||||", StringComparison.Ordinal))
                    {
                        i++;
                        while (i < lines.Length && !lines[i].StartsWith("=======", StringComparison.Ordinal))
                            i++;
                    }

                    // Skip separator
                    if (i < lines.Length && lines[i].StartsWith("=======", StringComparison.Ordinal))
                        i++;

                    // Collect theirs content
                    while (i < lines.Length && !lines[i].StartsWith(">>>>>>>", StringComparison.Ordinal))
                    {
                        region.TheirsContent.Add(lines[i]);
                        i++;
                    }

                    // End marker
                    if (i < lines.Length && lines[i].StartsWith(">>>>>>>", StringComparison.Ordinal))
                    {
                        region.EndLineInOriginal = i;
                        i++;
                    }

                    _conflictRegions.Add(region);
                }
                else
                {
                    i++;
                }
            }
        }

        private void BuildStaticPanels()
        {
            // Build MINE and THEIRS panels from original content - these never change
            var oursLines = new List<Models.TextDiffLine>();
            var theirsLines = new List<Models.TextDiffLine>();

            if (string.IsNullOrEmpty(_originalContent))
            {
                OursDiffLines = oursLines;
                TheirsDiffLines = theirsLines;
                return;
            }

            var lines = _originalContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            int oursLineNumber = 1;
            int theirsLineNumber = 1;
            int conflictIdx = 0;

            int i = 0;
            while (i < lines.Length)
            {
                var line = lines[i];

                if (line.StartsWith("<<<<<<<", StringComparison.Ordinal))
                {
                    // Track panel start line for this conflict
                    int panelStartLine = oursLines.Count;

                    // Start of conflict - add marker line placeholder
                    oursLines.Add(new Models.TextDiffLine());
                    theirsLines.Add(new Models.TextDiffLine());
                    i++;

                    // Collect mine content
                    var mineContent = new List<string>();
                    while (i < lines.Length &&
                           !lines[i].StartsWith("|||||||", StringComparison.Ordinal) &&
                           !lines[i].StartsWith("=======", StringComparison.Ordinal))
                    {
                        mineContent.Add(lines[i]);
                        i++;
                    }

                    // Skip diff3 base section
                    if (i < lines.Length && lines[i].StartsWith("|||||||", StringComparison.Ordinal))
                    {
                        i++;
                        while (i < lines.Length && !lines[i].StartsWith("=======", StringComparison.Ordinal))
                            i++;
                    }

                    // Skip separator
                    if (i < lines.Length && lines[i].StartsWith("=======", StringComparison.Ordinal))
                    {
                        oursLines.Add(new Models.TextDiffLine());
                        theirsLines.Add(new Models.TextDiffLine());
                        i++;
                    }

                    // Collect theirs content
                    var theirsContent = new List<string>();
                    while (i < lines.Length && !lines[i].StartsWith(">>>>>>>", StringComparison.Ordinal))
                    {
                        theirsContent.Add(lines[i]);
                        i++;
                    }

                    // Add mine content lines (with empty placeholders in theirs)
                    foreach (var mineLine in mineContent)
                    {
                        oursLines.Add(new Models.TextDiffLine(Models.TextDiffLineType.Deleted, mineLine, oursLineNumber++, 0));
                        theirsLines.Add(new Models.TextDiffLine());
                    }

                    // Add theirs content lines (with empty placeholders in ours)
                    foreach (var theirsLine in theirsContent)
                    {
                        oursLines.Add(new Models.TextDiffLine());
                        theirsLines.Add(new Models.TextDiffLine(Models.TextDiffLineType.Added, theirsLine, 0, theirsLineNumber++));
                    }

                    // Add end marker placeholder
                    if (i < lines.Length && lines[i].StartsWith(">>>>>>>", StringComparison.Ordinal))
                    {
                        oursLines.Add(new Models.TextDiffLine());
                        theirsLines.Add(new Models.TextDiffLine());
                        i++;
                    }

                    // Track panel end line for this conflict
                    int panelEndLine = oursLines.Count - 1;

                    // Store panel positions in conflict region
                    if (conflictIdx < _conflictRegions.Count)
                    {
                        _conflictRegions[conflictIdx].PanelStartLine = panelStartLine;
                        _conflictRegions[conflictIdx].PanelEndLine = panelEndLine;
                    }
                    conflictIdx++;
                }
                else
                {
                    // Normal line - same in both panels
                    oursLines.Add(new Models.TextDiffLine(Models.TextDiffLineType.Normal, line, oursLineNumber++, oursLineNumber - 1));
                    theirsLines.Add(new Models.TextDiffLine(Models.TextDiffLineType.Normal, line, theirsLineNumber++, theirsLineNumber - 1));
                    i++;
                }
            }

            OursDiffLines = oursLines;
            TheirsDiffLines = theirsLines;

            var maxLineNumber = Math.Max(oursLineNumber, theirsLineNumber);
            DiffMaxLineNumber = maxLineNumber;
        }

        private void BuildAlignedResultPanel()
        {
            // Build RESULT panel aligned with MINE/THEIRS panels
            // This ensures all three panels have the same number of lines for scroll sync
            var resultLines = new List<Models.TextDiffLine>();

            if (_oursDiffLines == null || _oursDiffLines.Count == 0)
            {
                ResultDiffLines = resultLines;
                return;
            }

            int resultLineNumber = 1;
            int currentLine = 0;
            int conflictIdx = 0;

            while (currentLine < _oursDiffLines.Count)
            {
                // Check if we're at a conflict region
                ConflictRegion currentRegion = null;
                if (conflictIdx < _conflictRegions.Count)
                {
                    var region = _conflictRegions[conflictIdx];
                    if (region.PanelStartLine == currentLine)
                        currentRegion = region;
                }

                if (currentRegion != null)
                {
                    int regionLines = currentRegion.PanelEndLine - currentRegion.PanelStartLine + 1;

                    if (currentRegion.ResolvedContent != null)
                    {
                        // Resolved - show resolved content + padding
                        foreach (var line in currentRegion.ResolvedContent)
                        {
                            resultLines.Add(new Models.TextDiffLine(
                                Models.TextDiffLineType.Normal, line, resultLineNumber, resultLineNumber));
                            resultLineNumber++;
                        }
                        // Pad with empty lines to match Mine/Theirs panel height
                        int padding = regionLines - currentRegion.ResolvedContent.Count;
                        for (int p = 0; p < padding; p++)
                            resultLines.Add(new Models.TextDiffLine());
                    }
                    else
                    {
                        // Unresolved - show conflict markers with content, aligned with Mine/Theirs
                        // First line: start marker placeholder (matches <<<<<< line)
                        resultLines.Add(new Models.TextDiffLine(Models.TextDiffLineType.Indicator, "<<<<<<< (unresolved)", 0, 0));

                        // Second line: separator placeholder (matches ======= line)
                        resultLines.Add(new Models.TextDiffLine(Models.TextDiffLineType.Indicator, "=======", 0, 0));

                        // Mine content lines (matches the deleted lines in Ours panel)
                        foreach (var line in currentRegion.OursContent)
                        {
                            resultLines.Add(new Models.TextDiffLine(
                                Models.TextDiffLineType.Deleted, line, 0, resultLineNumber++));
                        }

                        // Theirs content lines (matches the added lines in Theirs panel)
                        foreach (var line in currentRegion.TheirsContent)
                        {
                            resultLines.Add(new Models.TextDiffLine(
                                Models.TextDiffLineType.Added, line, 0, resultLineNumber++));
                        }

                        // End marker placeholder (matches >>>>>>> line)
                        resultLines.Add(new Models.TextDiffLine(Models.TextDiffLineType.Indicator, ">>>>>>> (unresolved)", 0, 0));
                    }

                    currentLine = currentRegion.PanelEndLine + 1;
                    conflictIdx++;
                }
                else
                {
                    // Normal line - copy from ours panel
                    var oursLine = _oursDiffLines[currentLine];
                    if (oursLine.Type == Models.TextDiffLineType.Normal)
                    {
                        resultLines.Add(new Models.TextDiffLine(
                            Models.TextDiffLineType.Normal, oursLine.Content,
                            resultLineNumber, resultLineNumber));
                        resultLineNumber++;
                    }
                    else
                    {
                        // Empty placeholder line (shouldn't happen outside conflicts, but handle it)
                        resultLines.Add(new Models.TextDiffLine());
                    }
                    currentLine++;
                }
            }

            ResultDiffLines = resultLines;
        }

        public void AcceptOurs()
        {
            if (_conflictRegions.Count == 0)
                return;

            bool anyResolved = false;
            foreach (var region in _conflictRegions)
            {
                if (!region.IsResolved)
                {
                    region.ResolvedContent = new List<string>(region.OursContent);
                    region.IsResolved = true;
                    anyResolved = true;
                }
            }

            if (anyResolved)
            {
                RebuildResultContent();
                BuildAlignedResultPanel();
                UpdateConflictInfo();
                IsModified = true;
            }
        }

        public void AcceptTheirs()
        {
            if (_conflictRegions.Count == 0)
                return;

            bool anyResolved = false;
            foreach (var region in _conflictRegions)
            {
                if (!region.IsResolved)
                {
                    region.ResolvedContent = new List<string>(region.TheirsContent);
                    region.IsResolved = true;
                    anyResolved = true;
                }
            }

            if (anyResolved)
            {
                RebuildResultContent();
                BuildAlignedResultPanel();
                UpdateConflictInfo();
                IsModified = true;
            }
        }

        public void AcceptCurrentOurs()
        {
            if (_currentConflictIndex < 0 || _currentConflictIndex >= _conflictRegions.Count)
                return;

            var region = _conflictRegions[_currentConflictIndex];
            if (region.IsResolved)
                return;

            region.ResolvedContent = new List<string>(region.OursContent);
            region.IsResolved = true;

            RebuildResultContent();
            BuildAlignedResultPanel();
            UpdateConflictInfo();
            IsModified = true;
        }

        public void AcceptCurrentTheirs()
        {
            if (_currentConflictIndex < 0 || _currentConflictIndex >= _conflictRegions.Count)
                return;

            var region = _conflictRegions[_currentConflictIndex];
            if (region.IsResolved)
                return;

            region.ResolvedContent = new List<string>(region.TheirsContent);
            region.IsResolved = true;

            RebuildResultContent();
            BuildAlignedResultPanel();
            UpdateConflictInfo();
            IsModified = true;
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
                UpdateResolvedRanges();
                return;
            }

            // Count unresolved conflicts in current content
            var markers = Commands.QueryConflictContent.GetConflictMarkers(_resultContent);
            var conflictStarts = markers.Where(m => m.Type == Models.ConflictMarkerType.Start).ToList();

            int unresolvedCount = conflictStarts.Count;
            UnresolvedConflictCount = unresolvedCount;

            // Total conflicts is the original count (never changes)
            _totalConflicts = _conflictRegions.Count;

            // Mark which original conflicts are resolved
            // A conflict is resolved if its start marker no longer exists in _resultContent
            var currentLines = _resultContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            int unresolvedIdx = 0;
            foreach (var region in _conflictRegions)
            {
                // Check if this conflict's content is still present as a conflict (has markers)
                region.IsResolved = true;
                for (int i = 0; i < currentLines.Length; i++)
                {
                    if (currentLines[i].StartsWith("<<<<<<<", StringComparison.Ordinal))
                    {
                        if (unresolvedIdx < unresolvedCount)
                        {
                            // This is an unresolved conflict - check if it matches this region
                            // by comparing ours content
                            int j = i + 1;
                            var currentOurs = new List<string>();
                            while (j < currentLines.Length &&
                                   !currentLines[j].StartsWith("|||||||", StringComparison.Ordinal) &&
                                   !currentLines[j].StartsWith("=======", StringComparison.Ordinal))
                            {
                                currentOurs.Add(currentLines[j]);
                                j++;
                            }

                            if (currentOurs.Count == region.OursContent.Count &&
                                currentOurs.SequenceEqual(region.OursContent))
                            {
                                region.IsResolved = false;
                                break;
                            }
                        }
                    }
                }
            }

            // Update resolved ranges for UI desaturation
            UpdateResolvedRanges();

            // Find the first unresolved conflict for current index
            if (unresolvedCount > 0)
            {
                if (CurrentConflictIndex < 0)
                {
                    // Find first unresolved
                    for (int i = 0; i < _conflictRegions.Count; i++)
                    {
                        if (!_conflictRegions[i].IsResolved)
                        {
                            CurrentConflictIndex = i;
                            break;
                        }
                    }
                }
                else if (CurrentConflictIndex >= _conflictRegions.Count ||
                         _conflictRegions[CurrentConflictIndex].IsResolved)
                {
                    // Current conflict was resolved, find next unresolved
                    int found = -1;
                    for (int i = CurrentConflictIndex + 1; i < _conflictRegions.Count; i++)
                    {
                        if (!_conflictRegions[i].IsResolved)
                        {
                            found = i;
                            break;
                        }
                    }
                    if (found < 0)
                    {
                        // Try from beginning
                        for (int i = 0; i < _conflictRegions.Count; i++)
                        {
                            if (!_conflictRegions[i].IsResolved)
                            {
                                found = i;
                                break;
                            }
                        }
                    }
                    CurrentConflictIndex = found;
                }
                UpdateCurrentConflictLine();
            }
            else
            {
                CurrentConflictIndex = -1;
                CurrentConflictLine = -1;
                CurrentConflictStartLine = -1;
                CurrentConflictEndLine = -1;
            }
        }

        private void UpdateResolvedRanges()
        {
            // Build list of all conflict ranges for UI fading of non-current conflicts
            var allRanges = new List<(int Start, int End)>();

            foreach (var region in _conflictRegions)
            {
                if (region.PanelStartLine >= 0 && region.PanelEndLine >= 0)
                {
                    allRanges.Add((region.PanelStartLine, region.PanelEndLine));
                }
            }

            AllConflictRanges = allRanges;
        }

        private void RebuildResultContent()
        {
            // Rebuild _resultContent based on _originalContent and resolved regions
            // This keeps _resultContent in sync with the resolved state for saving
            if (string.IsNullOrEmpty(_originalContent))
            {
                _resultContent = string.Empty;
                return;
            }

            var lines = _originalContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var resultLines = new List<string>();

            int i = 0;
            int conflictIdx = 0;

            while (i < lines.Length)
            {
                var line = lines[i];

                if (line.StartsWith("<<<<<<<", StringComparison.Ordinal))
                {
                    // Get the current conflict region
                    ConflictRegion region = null;
                    if (conflictIdx < _conflictRegions.Count)
                        region = _conflictRegions[conflictIdx];

                    if (region != null && region.ResolvedContent != null)
                    {
                        // Conflict is resolved - add resolved content
                        resultLines.AddRange(region.ResolvedContent);

                        // Skip past the entire conflict in original content
                        while (i < lines.Length && !lines[i].StartsWith(">>>>>>>", StringComparison.Ordinal))
                            i++;
                        i++; // Skip the >>>>>>> line
                    }
                    else
                    {
                        // Conflict is not resolved - keep original conflict markers
                        while (i < lines.Length)
                        {
                            resultLines.Add(lines[i]);
                            if (lines[i].StartsWith(">>>>>>>", StringComparison.Ordinal))
                            {
                                i++;
                                break;
                            }
                            i++;
                        }
                    }
                    conflictIdx++;
                }
                else
                {
                    // Normal line - copy as-is
                    resultLines.Add(line);
                    i++;
                }
            }

            _resultContent = string.Join(Environment.NewLine, resultLines);
        }

        private void UpdateCurrentConflictLine()
        {
            if (_currentConflictIndex < 0 || _currentConflictIndex >= _conflictRegions.Count)
            {
                CurrentConflictLine = -1;
                CurrentConflictStartLine = -1;
                CurrentConflictEndLine = -1;
                return;
            }

            // Use the pre-computed panel positions from the conflict region
            var region = _conflictRegions[_currentConflictIndex];
            CurrentConflictLine = region.PanelStartLine;
            CurrentConflictStartLine = region.PanelStartLine;
            CurrentConflictEndLine = region.PanelEndLine;
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
        private List<ConflictRegion> _conflictRegions = [];
        private List<(int Start, int End)> _allConflictRanges = [];
        private Vector _scrollOffset = Vector.Zero;
    }
}
