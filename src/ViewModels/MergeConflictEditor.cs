using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class MergeConflictEditor : ObservableObject
    {
        public string FilePath
        {
            get => _filePath;
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public string Error
        {
            get => _error;
            private set => SetProperty(ref _error, value);
        }

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

        public string StatusText
        {
            get
            {
                if (_unresolvedConflictCount > 0)
                    return App.Text("MergeConflictEditor.ConflictsRemaining", _unresolvedConflictCount);
                return App.Text("MergeConflictEditor.AllResolved");
            }
        }

        public int CurrentConflictIndex
        {
            get => _currentConflictIndex;
            private set => SetProperty(ref _currentConflictIndex, value);
        }

        public Vector ScrollOffset
        {
            get => _scrollOffset;
            set => SetProperty(ref _scrollOffset, value);
        }

        public Models.ConflictSelectedChunk SelectedChunk
        {
            get => _selectedChunk;
            set => SetProperty(ref _selectedChunk, value);
        }

        public IReadOnlyList<Models.ConflictRegion> ConflictRegions => _conflictRegions;
        public bool HasUnresolvedConflicts => _unresolvedConflictCount > 0;
        public bool HasUnsavedChanges => _isModified && !_resultContent.Equals(_originalContent, StringComparison.Ordinal);
        public bool CanSave => _unresolvedConflictCount == 0 && _isModified;

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
                var workingCopyPath = Path.Combine(_repo.FullPath, _filePath);
                var workingCopyContent = string.Empty;
                if (File.Exists(workingCopyPath))
                    workingCopyContent = await File.ReadAllTextAsync(workingCopyPath).ConfigureAwait(false);

                if (workingCopyContent.IndexOf('\0', StringComparison.Ordinal) >= 0)
                    throw new Exception("Binary file is not supported!!!");

                Dispatcher.UIThread.Post(() =>
                {
                    _resultContent = workingCopyContent;
                    _originalContent = workingCopyContent;
                    _isModified = false;

                    ParseOriginalConflicts(workingCopyContent);
                    BuildStaticPanels();
                    BuildAlignedResultPanel();
                    UpdateConflictInfo();
                    IsLoading = false;
                });
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    IsLoading = false;
                    Error = ex.Message;
                });
            }
        }

        public Models.ConflictLineState GetLineState(int line)
        {
            if (line >= 0 && line < _lineStates.Count)
                return _lineStates[line];
            return Models.ConflictLineState.Normal;
        }

        public void AcceptOursAtIndex(int conflictIndex)
        {
            if (conflictIndex < 0 || conflictIndex >= _conflictRegions.Count)
                return;

            var region = _conflictRegions[conflictIndex];
            if (region.IsResolved)
                return;

            region.ResolvedContent = new List<string>(region.OursContent);
            region.IsResolved = true;
            region.ResolutionType = Models.ConflictResolution.UseOurs;
            Resolve();
        }

        public void AcceptTheirsAtIndex(int conflictIndex)
        {
            if (conflictIndex < 0 || conflictIndex >= _conflictRegions.Count)
                return;

            var region = _conflictRegions[conflictIndex];
            if (region.IsResolved)
                return;

            region.ResolvedContent = new List<string>(region.TheirsContent);
            region.IsResolved = true;
            region.ResolutionType = Models.ConflictResolution.UseTheirs;
            Resolve();
        }

        public void AcceptBothMineFirstAtIndex(int conflictIndex)
        {
            if (conflictIndex < 0 || conflictIndex >= _conflictRegions.Count)
                return;

            var region = _conflictRegions[conflictIndex];
            if (region.IsResolved)
                return;

            var combined = new List<string>(region.OursContent);
            combined.AddRange(region.TheirsContent);
            region.ResolvedContent = combined;
            region.IsResolved = true;
            region.ResolutionType = Models.ConflictResolution.UseBothMineFirst;
            Resolve();
        }

        public void AcceptBothTheirsFirstAtIndex(int conflictIndex)
        {
            if (conflictIndex < 0 || conflictIndex >= _conflictRegions.Count)
                return;

            var region = _conflictRegions[conflictIndex];
            if (region.IsResolved)
                return;

            var combined = new List<string>(region.TheirsContent);
            combined.AddRange(region.OursContent);
            region.ResolvedContent = combined;
            region.IsResolved = true;
            region.ResolutionType = Models.ConflictResolution.UseBothTheirsFirst;
            Resolve();
        }

        public void UndoResolutionAtIndex(int conflictIndex)
        {
            if (conflictIndex < 0 || conflictIndex >= _conflictRegions.Count)
                return;

            var region = _conflictRegions[conflictIndex];
            if (!region.IsResolved)
                return;

            region.ResolvedContent = null;
            region.IsResolved = false;
            region.ResolutionType = Models.ConflictResolution.None;
            Resolve();
        }

        public void GotoPrevConflict()
        {
            if (_unresolvedConflictCount == 0 || _conflictRegions.Count == 0)
                return;

            // Handle edge case where no conflict is currently selected
            int startIndex = _currentConflictIndex >= 0 ? _currentConflictIndex : 0;

            // Search for the previous unresolved conflict with wrap-around
            int index = startIndex - 1;
            if (index < 0)
                index = _conflictRegions.Count - 1;

            int iterations = 0;
            while (iterations < _conflictRegions.Count)
            {
                if (!_conflictRegions[index].IsResolved)
                {
                    CurrentConflictIndex = index;
                    return;
                }
                index--;
                if (index < 0)
                    index = _conflictRegions.Count - 1;
                iterations++;
            }
        }

        public void GotoNextConflict()
        {
            if (_unresolvedConflictCount == 0 || _conflictRegions.Count == 0)
                return;

            // Handle edge case where no conflict is currently selected
            int startIndex = _currentConflictIndex >= 0 ? _currentConflictIndex : -1;

            // Search for the next unresolved conflict with wrap-around
            int index = (startIndex + 1) % _conflictRegions.Count;

            int iterations = 0;
            while (iterations < _conflictRegions.Count)
            {
                if (!_conflictRegions[index].IsResolved)
                {
                    CurrentConflictIndex = index;
                    return;
                }
                index = (index + 1) % _conflictRegions.Count;
                iterations++;
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
                _isModified = false;
                _originalContent = _resultContent;

                return true;
            }
            catch (Exception ex)
            {
                App.RaiseException(_repo.FullPath, $"Failed to save and stage: {ex.Message}");
                return false;
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
                    var region = new Models.ConflictRegion { StartLineInOriginal = i };
                    // Capture the start marker (e.g., "<<<<<<< HEAD")
                    region.StartMarker = line;
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

                    // Capture separator marker
                    if (i < lines.Length && lines[i].StartsWith("=======", StringComparison.Ordinal))
                    {
                        region.SeparatorMarker = lines[i];
                        i++;
                    }

                    // Collect theirs content
                    while (i < lines.Length && !lines[i].StartsWith(">>>>>>>", StringComparison.Ordinal))
                    {
                        region.TheirsContent.Add(lines[i]);
                        i++;
                    }

                    // Capture end marker (e.g., ">>>>>>> feature-branch")
                    if (i < lines.Length && lines[i].StartsWith(">>>>>>>", StringComparison.Ordinal))
                    {
                        region.EndMarker = lines[i];
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
            _lineStates.Clear();

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
                Models.ConflictRegion currentRegion = null;
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
                        var oldLineCount = resultLines.Count;

                        // Resolved - show resolved content with color based on resolution type
                        if (currentRegion.ResolutionType == Models.ConflictResolution.UseBothMineFirst)
                        {
                            // First portion is Mine (Deleted color), second is Theirs (Added color)
                            int mineCount = currentRegion.OursContent.Count;
                            for (int i = 0; i < currentRegion.ResolvedContent.Count; i++)
                            {
                                var lineType = i < mineCount
                                    ? Models.TextDiffLineType.Deleted
                                    : Models.TextDiffLineType.Added;
                                resultLines.Add(new Models.TextDiffLine(
                                    lineType, currentRegion.ResolvedContent[i], resultLineNumber, resultLineNumber));
                                resultLineNumber++;
                            }
                        }
                        else if (currentRegion.ResolutionType == Models.ConflictResolution.UseBothTheirsFirst)
                        {
                            // First portion is Theirs (Added color), second is Mine (Deleted color)
                            int theirsCount = currentRegion.TheirsContent.Count;
                            for (int i = 0; i < currentRegion.ResolvedContent.Count; i++)
                            {
                                var lineType = i < theirsCount
                                    ? Models.TextDiffLineType.Added
                                    : Models.TextDiffLineType.Deleted;
                                resultLines.Add(new Models.TextDiffLine(
                                    lineType, currentRegion.ResolvedContent[i], resultLineNumber, resultLineNumber));
                                resultLineNumber++;
                            }
                        }
                        else
                        {
                            var lineType = currentRegion.ResolutionType switch
                            {
                                Models.ConflictResolution.UseOurs => Models.TextDiffLineType.Deleted,   // Mine color
                                Models.ConflictResolution.UseTheirs => Models.TextDiffLineType.Added,  // Theirs color
                                _ => Models.TextDiffLineType.Normal
                            };

                            foreach (var line in currentRegion.ResolvedContent)
                            {
                                resultLines.Add(new Models.TextDiffLine(
                                    lineType, line, resultLineNumber, resultLineNumber));
                                resultLineNumber++;
                            }
                        }
                        // Pad with empty lines to match Mine/Theirs panel height
                        int padding = regionLines - currentRegion.ResolvedContent.Count;
                        for (int p = 0; p < padding; p++)
                            resultLines.Add(new Models.TextDiffLine());

                        int added = resultLines.Count - oldLineCount;
                        _lineStates.Add(Models.ConflictLineState.ResolvedBlockStart);
                        for (var i = 0; i < added - 2; i++)
                            _lineStates.Add(Models.ConflictLineState.ResolvedBlock);
                        _lineStates.Add(Models.ConflictLineState.ResolvedBlockEnd);
                    }
                    else
                    {
                        // Unresolved - show conflict markers with content, aligned with Mine/Theirs
                        // First line: start marker (use real marker from file)
                        resultLines.Add(new Models.TextDiffLine(Models.TextDiffLineType.Indicator, currentRegion.StartMarker, 0, 0));
                        _lineStates.Add(Models.ConflictLineState.ConflictBlockStart);

                        // Mine content lines (matches the deleted lines in Ours panel)
                        foreach (var line in currentRegion.OursContent)
                        {
                            resultLines.Add(new Models.TextDiffLine(
                                Models.TextDiffLineType.Deleted, line, 0, resultLineNumber++));
                            _lineStates.Add(Models.ConflictLineState.ConflictBlock);
                        }

                        // Separator marker between Mine and Theirs
                        resultLines.Add(new Models.TextDiffLine(Models.TextDiffLineType.Indicator, currentRegion.SeparatorMarker, 0, 0));
                        _lineStates.Add(Models.ConflictLineState.ConflictBlock);

                        // Theirs content lines (matches the added lines in Theirs panel)
                        foreach (var line in currentRegion.TheirsContent)
                        {
                            resultLines.Add(new Models.TextDiffLine(
                                Models.TextDiffLineType.Added, line, 0, resultLineNumber++));
                            _lineStates.Add(Models.ConflictLineState.ConflictBlock);
                        }

                        // End marker (use real marker from file)
                        resultLines.Add(new Models.TextDiffLine(Models.TextDiffLineType.Indicator, currentRegion.EndMarker, 0, 0));
                        _lineStates.Add(Models.ConflictLineState.ConflictBlockEnd);
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

                    _lineStates.Add(Models.ConflictLineState.Normal);
                    currentLine++;
                }
            }

            ResultDiffLines = resultLines;
        }

        private void Resolve()
        {
            var scroll = _scrollOffset;
            RebuildResultContent();
            BuildAlignedResultPanel();
            UpdateConflictInfo();
            SelectedChunk = null;
            ScrollOffset = scroll;
        }

        private void UpdateConflictInfo()
        {
            if (string.IsNullOrEmpty(_resultContent))
            {
                UnresolvedConflictCount = 0;
                CurrentConflictIndex = -1;
                return;
            }

            var markers = GetConflictMarkers(_resultContent);
            var conflictStarts = new List<Models.ConflictMarkerInfo>();
            foreach (var m in markers)
            {
                if (m.Type == Models.ConflictMarkerType.Start)
                    conflictStarts.Add(m);
            }

            int unresolvedCount = conflictStarts.Count;
            UnresolvedConflictCount = unresolvedCount;

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

                            if (currentOurs.Count != region.OursContent.Count)
                                continue;

                            var allEquals = true;
                            for (var k = 0; k < currentOurs.Count; k++)
                            {
                                if (!currentOurs[k].Equals(region.OursContent[k], StringComparison.Ordinal))
                                {
                                    allEquals = false;
                                    break;
                                }
                            }

                            if (allEquals)
                            {
                                region.IsResolved = false;
                                break;
                            }
                        }
                    }
                }
            }

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
            }
            else
            {
                CurrentConflictIndex = -1;
            }
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
                    Models.ConflictRegion region = null;
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
            _isModified = true;
        }

        private List<Models.ConflictMarkerInfo> GetConflictMarkers(string content)
        {
            var markers = new List<Models.ConflictMarkerInfo>();
            if (string.IsNullOrEmpty(content))
                return markers;

            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            int offset = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var lineStart = offset;
                var lineEnd = offset + line.Length;

                if (line.StartsWith("<<<<<<<", StringComparison.Ordinal))
                {
                    markers.Add(new Models.ConflictMarkerInfo
                    {
                        LineNumber = i,
                        StartOffset = lineStart,
                        EndOffset = lineEnd,
                        Type = Models.ConflictMarkerType.Start
                    });
                }
                else if (line.StartsWith("|||||||", StringComparison.Ordinal))
                {
                    markers.Add(new Models.ConflictMarkerInfo
                    {
                        LineNumber = i,
                        StartOffset = lineStart,
                        EndOffset = lineEnd,
                        Type = Models.ConflictMarkerType.Base
                    });
                }
                else if (line.StartsWith("=======", StringComparison.Ordinal))
                {
                    markers.Add(new Models.ConflictMarkerInfo
                    {
                        LineNumber = i,
                        StartOffset = lineStart,
                        EndOffset = lineEnd,
                        Type = Models.ConflictMarkerType.Separator
                    });
                }
                else if (line.StartsWith(">>>>>>>", StringComparison.Ordinal))
                {
                    markers.Add(new Models.ConflictMarkerInfo
                    {
                        LineNumber = i,
                        StartOffset = lineStart,
                        EndOffset = lineEnd,
                        Type = Models.ConflictMarkerType.End
                    });
                }

                // Account for line ending (approximate)
                offset = lineEnd + (i < lines.Length - 1 ? Environment.NewLine.Length : 0);
            }

            return markers;
        }

        private readonly Repository _repo;
        private readonly string _filePath;
        private string _resultContent = string.Empty;
        private string _originalContent = string.Empty;
        private bool _isLoading = false;
        private bool _isModified = false;
        private int _unresolvedConflictCount = 0;
        private int _currentConflictIndex = -1;
        private int _diffMaxLineNumber = 0;
        private List<Models.TextDiffLine> _oursDiffLines = [];
        private List<Models.TextDiffLine> _theirsDiffLines = [];
        private List<Models.TextDiffLine> _resultDiffLines = [];
        private List<Models.ConflictRegion> _conflictRegions = [];
        private List<Models.ConflictLineState> _lineStates = [];
        private Vector _scrollOffset = Vector.Zero;
        private Models.ConflictSelectedChunk _selectedChunk;
        private string _error = string.Empty;
    }
}
