using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class MergeConflictEditor : ObservableObject
    {
        public string FilePath
        {
            get => _filePath;
        }

        public object Mine
        {
            get;
        }

        public object Theirs
        {
            get;
        }

        public string Error
        {
            get => _error;
            private set => SetProperty(ref _error, value);
        }

        public List<Models.ConflictLine> OursLines
        {
            get => _oursLines;
            private set => SetProperty(ref _oursLines, value);
        }

        public List<Models.ConflictLine> TheirsLines
        {
            get => _theirsLines;
            private set => SetProperty(ref _theirsLines, value);
        }

        public TextDocument ResultDocument
        {
            get;
        } = new TextDocument();

        public int MaxLineNumber
        {
            get => _maxLineNumber;
            private set => SetProperty(ref _maxLineNumber, value);
        }

        public int UnsolvedCount
        {
            get => _unsolvedCount;
            private set
            {
                if (SetProperty(ref _unsolvedCount, value))
                    OnPropertyChanged(nameof(NavigationDesc));
            }
        }

        public int CurrentConflictIndex
        {
            get => _currentConflictIndex;
            private set
            {
                if (SetProperty(ref _currentConflictIndex, value))
                    OnPropertyChanged(nameof(NavigationDesc));
            }
        }

        public string NavigationDesc
        {
            get
            {
                var unsolved = _unsolvedCount == 0 ?
                    App.Text("MergeConflictEditor.AllResolved") :
                    App.Text("MergeConflictEditor.ConflictsRemaining", _unsolvedCount);

                if (_currentConflictIndex < 0 || _conflictRegions.Count == 0)
                    return unsolved;

                var position = App.Text("MergeConflictEditor.ConflictPosition", _currentConflictIndex + 1, _conflictRegions.Count);
                return $"{position} · {unsolved}";
            }
        }

        public bool IsDirty
        {
            get;
            private set;
        }

        public Models.ConflictSelectedChunk SelectedChunk
        {
            get => _selectedChunk;
            set => SetProperty(ref _selectedChunk, value);
        }

        public IReadOnlyList<Models.ConflictRegion> ConflictRegions
        {
            get => _conflictRegions;
        }

        public event Action RegionsChanged;

        public MergeConflictEditor(Repository repo, Models.Commit head, string filePath)
        {
            _repo = repo;
            _filePath = filePath;

            (Mine, Theirs) = repo.InProgressContext switch
            {
                CherryPickInProgress cherryPick => (head, cherryPick.Head),
                RebaseInProgress rebase => (rebase.Onto, rebase.StoppedAt),
                AmInProgress applying => (head, (object)"Patch"),
                RevertInProgress revert => (head, revert.Head),
                MergeInProgress merge => (head, merge.Source),
                _ => (head, (object)"Stash or Patch"),
            };

            var workingCopyPath = Path.Combine(_repo.FullPath, _filePath);
            var workingCopyContent = string.Empty;
            if (File.Exists(workingCopyPath))
                workingCopyContent = File.ReadAllText(workingCopyPath);

            if (workingCopyContent.IndexOf('\0', StringComparison.Ordinal) >= 0)
            {
                _error = "Binary file is not supported.";
                return;
            }

            ParseOriginalContent(workingCopyContent);
            BuildResultDocument();
            RefreshRegionStates();

            ResultDocument.Changing += OnResultDocumentChanging;
            ResultDocument.Changed += OnResultDocumentChanged;
        }

        public Models.ConflictLineDecoration GetSideLineDecoration(Models.ConflictPanelType side, int line)
        {
            var lines = side == Models.ConflictPanelType.Ours ? _oursLines : _theirsLines;
            if (line < 0 || line >= lines.Count)
                return default;

            var info = lines[line];
            if (info.RegionIndex < 0)
                return default;

            var r = _conflictRegions[info.RegionIndex];
            var count = side == Models.ConflictPanelType.Ours ? r.OursRowCount : r.TheirsRowCount;
            return new(Models.ConflictLineType.None, true, r.IsResolved, info.IndexInSide == 0, info.IndexInSide == count - 1);
        }

        public Models.ConflictLineDecoration GetResultLineDecoration(int line)
        {
            if (line >= 0 && line < _resultDecorations.Length)
                return _resultDecorations[line];
            return default;
        }

        public (int FirstLine, int LineCount) GetResultRange(int regionIdx)
        {
            var range = _ranges[regionIdx];
            return (range.FirstLine, range.LineCount);
        }

        public int FindRegionByResultLine(int line)
        {
            for (var i = 0; i < _ranges.Count; i++)
            {
                var range = _ranges[i];
                if (line >= range.FirstLine && line < range.FirstLine + range.LineCount)
                    return i;
            }

            return -1;
        }

        public (int FirstLine, int LineCount) GetRegionRange(Models.ConflictPanelType panel, int regionIdx)
        {
            var r = _conflictRegions[regionIdx];
            return panel switch
            {
                Models.ConflictPanelType.Ours => (r.OursFirst, r.OursRowCount),
                Models.ConflictPanelType.Theirs => (r.TheirsFirst, r.TheirsRowCount),
                _ => (_ranges[regionIdx].FirstLine, _ranges[regionIdx].LineCount),
            };
        }

        public int GetPanelLineCount(Models.ConflictPanelType panel)
        {
            return panel switch
            {
                Models.ConflictPanelType.Ours => _oursLines.Count,
                Models.ConflictPanelType.Theirs => _theirsLines.Count,
                _ => ResultDocument.LineCount,
            };
        }

        // Maps a line between panels. Unchanged lines map one-to-one, lines inside a conflict are clamped to
        // the size of that conflict in the target panel.
        public int MapLine(Models.ConflictPanelType from, Models.ConflictPanelType to, int line)
        {
            if (from == to)
                return line;

            var prevSrcEnd = 0;
            var prevDstEnd = 0;
            for (var i = 0; i < _conflictRegions.Count; i++)
            {
                var (srcFirst, srcCount) = GetRegionRange(from, i);
                var (dstFirst, dstCount) = GetRegionRange(to, i);
                if (line < srcFirst)
                    break;

                if (line < srcFirst + srcCount)
                    return dstFirst + Math.Min(line - srcFirst, Math.Max(dstCount - 1, 0));

                prevSrcEnd = srcFirst + srcCount;
                prevDstEnd = dstFirst + dstCount;
            }

            return Math.Clamp(prevDstEnd + (line - prevSrcEnd), 0, Math.Max(GetPanelLineCount(to) - 1, 0));
        }

        public bool IsPicked(int regionIdx, Models.ConflictPanelType side, int index, out int order)
        {
            var picks = _conflictRegions[regionIdx].Picks;
            order = picks.FindIndex(p => p.Side == side && p.Index == index);
            return order >= 0;
        }

        public void SetPick(int regionIdx, Models.ConflictPanelType side, int index, bool picked)
        {
            var r = _conflictRegions[regionIdx];
            if (r.IsCustom)
                return;

            var existing = r.Picks.FindIndex(p => p.Side == side && p.Index == index);
            if (picked == existing >= 0)
                return;

            if (existing >= 0)
                r.Picks.RemoveAt(existing);
            else
                r.Picks.Add(new(side, index));

            r.HasChoice = r.Picks.Count > 0;
            ApplyRegion(regionIdx);
            CurrentConflictIndex = regionIdx;
            OnRegionsChanged();
        }

        public void Resolve(object param)
        {
            if (_selectedChunk == null || param is not Models.ConflictResolution resolution)
                return;

            ResolveRegion(_selectedChunk.ConflictIndex, resolution);
        }

        public void ResolveRegion(int regionIdx, Models.ConflictResolution resolution)
        {
            var r = _conflictRegions[regionIdx];
            r.Picks.Clear();
            r.IsCustom = false;
            r.HasChoice = resolution != Models.ConflictResolution.None;

            if (resolution is Models.ConflictResolution.UseOurs or Models.ConflictResolution.UseBothMineFirst)
                AddAllPicks(r, Models.ConflictPanelType.Ours);

            if (resolution is Models.ConflictResolution.UseTheirs or Models.ConflictResolution.UseBothMineFirst or Models.ConflictResolution.UseBothTheirsFirst)
                AddAllPicks(r, Models.ConflictPanelType.Theirs);

            if (resolution == Models.ConflictResolution.UseBothTheirsFirst)
                AddAllPicks(r, Models.ConflictPanelType.Ours);

            ApplyRegion(regionIdx);
            CurrentConflictIndex = regionIdx;
            SelectedChunk = null;
            OnRegionsChanged();
        }

        public int GotoConflict(bool next)
        {
            if (_conflictRegions.Count == 0)
                return -1;

            var candidates = new List<int>();
            for (var i = 0; i < _conflictRegions.Count; i++)
            {
                if (!_conflictRegions[i].IsResolved)
                    candidates.Add(i);
            }

            if (candidates.Count == 0)
            {
                for (var i = 0; i < _conflictRegions.Count; i++)
                    candidates.Add(i);
            }

            var target = next ? candidates[0] : candidates[^1];
            if (next)
            {
                foreach (var c in candidates)
                {
                    if (c > _currentConflictIndex)
                    {
                        target = c;
                        break;
                    }
                }
            }
            else
            {
                for (var i = candidates.Count - 1; i >= 0; i--)
                {
                    if (candidates[i] < _currentConflictIndex)
                    {
                        target = candidates[i];
                        break;
                    }
                }
            }

            CurrentConflictIndex = target;
            return target;
        }

        public async Task<bool> SaveAndStageAsync()
        {
            if (_conflictRegions.Count == 0 && !IsDirty)
                return true;

            if (_unsolvedCount > 0)
            {
                Error = "Cannot save: there are still unresolved conflicts.";
                return false;
            }

            try
            {
                // Write merged content to file
                var fullPath = Path.Combine(_repo.FullPath, _filePath);
                await File.WriteAllTextAsync(fullPath, ResultDocument.Text);

                // Stage the file
                var pathSpecFile = Path.GetTempFileName();
                await File.WriteAllTextAsync(pathSpecFile, _filePath);
                await new Commands.Add(_repo.FullPath, pathSpecFile).ExecAsync();
                File.Delete(pathSpecFile);

                _repo.MarkWorkingCopyDirtyManually();
                return true;
            }
            catch (Exception ex)
            {
                Error = $"Failed to save and stage: {ex.Message}";
                return false;
            }
        }

        public void ClearErrorMessage()
        {
            Error = string.Empty;
        }

        private void ParseOriginalContent(string content)
        {
            _conflictRegions.Clear();
            _layout.Clear();

            if (string.IsNullOrEmpty(content))
                return;

            var lines = content.Split('\n', StringSplitOptions.None);
            var oursLines = new List<Models.ConflictLine>();
            var theirsLines = new List<Models.ConflictLine>();
            int oursLineNumber = 1;
            int theirsLineNumber = 1;
            int i = 0;

            while (i < lines.Length)
            {
                var line = lines[i];

                if (line.StartsWith("<<<<<<<", StringComparison.Ordinal))
                {
                    var regionIdx = _conflictRegions.Count;
                    var region = new Models.ConflictRegion
                    {
                        OursFirst = oursLines.Count,
                        TheirsFirst = theirsLines.Count,
                        StartMarker = line,
                    };
                    i++;

                    // Collect ours content
                    while (i < lines.Length &&
                           !lines[i].StartsWith("|||||||", StringComparison.Ordinal) &&
                           !lines[i].StartsWith("=======", StringComparison.Ordinal))
                    {
                        line = lines[i];
                        oursLines.Add(new(Models.ConflictLineType.Ours, line, oursLineNumber++) { RegionIndex = regionIdx, IndexInSide = region.OursContent.Count });
                        region.OursContent.Add(line);
                        i++;
                    }

                    if (region.OursContent.Count == 0)
                        oursLines.Add(new(Models.ConflictLineType.None, string.Empty) { RegionIndex = regionIdx, IndexInSide = 0 });

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
                        line = lines[i];
                        theirsLines.Add(new(Models.ConflictLineType.Theirs, line, theirsLineNumber++) { RegionIndex = regionIdx, IndexInSide = region.TheirsContent.Count });
                        region.TheirsContent.Add(line);
                        i++;
                    }

                    if (region.TheirsContent.Count == 0)
                        theirsLines.Add(new(Models.ConflictLineType.None, string.Empty) { RegionIndex = regionIdx, IndexInSide = 0 });

                    // Capture end marker (e.g., ">>>>>>> feature-branch")
                    if (i < lines.Length && lines[i].StartsWith(">>>>>>>", StringComparison.Ordinal))
                    {
                        region.EndMarker = lines[i];
                        i++;
                    }

                    region.IsAtEnd = i >= lines.Length;
                    _conflictRegions.Add(region);
                    _layout.Add((null, regionIdx));
                }
                else
                {
                    oursLines.Add(new(Models.ConflictLineType.Common, line, oursLineNumber));
                    theirsLines.Add(new(Models.ConflictLineType.Common, line, theirsLineNumber));
                    _layout.Add((line, -1));
                    i++;
                    oursLineNumber++;
                    theirsLineNumber++;
                }
            }

            MaxLineNumber = Math.Max(oursLineNumber, theirsLineNumber);
            OursLines = oursLines;
            TheirsLines = theirsLines;
        }

        private void BuildResultDocument()
        {
            var builder = new StringBuilder();
            var offsets = new List<(int Start, int End)>();
            _ranges.Clear();
            foreach (var (common, regionIdx) in _layout)
            {
                if (regionIdx >= 0)
                {
                    var range = new RegionRange();
                    var start = builder.Length;
                    foreach (var line in ComposeRegion(_conflictRegions[regionIdx], range.LineTypes))
                        builder.Append(line).Append('\n');

                    offsets.Add((start, builder.Length));
                    _ranges.Add(range);
                }
                else
                {
                    builder.Append(common).Append('\n');
                }
            }

            if (builder.Length > 0)
                builder.Length--; // Remove extra '\n'

            ResultDocument.Text = builder.ToString();
            ResultDocument.UndoStack.ClearAll();

            for (var i = 0; i < _ranges.Count; i++)
            {
                _ranges[i].Start = CreateAnchor(offsets[i].Start);
                _ranges[i].End = CreateAnchor(Math.Min(offsets[i].End, builder.Length));
            }
        }

        private List<string> ComposeRegion(Models.ConflictRegion r, List<Models.ConflictLineType> types)
        {
            var lines = new List<string>();
            types.Clear();

            if (r.HasChoice)
            {
                foreach (var pick in r.Picks)
                {
                    var isOurs = pick.Side == Models.ConflictPanelType.Ours;
                    var content = isOurs ? r.OursContent : r.TheirsContent;

                    // The placeholder row of an empty side contributes no lines.
                    if (pick.Index >= content.Count)
                        continue;

                    lines.Add(content[pick.Index]);
                    types.Add(isOurs ? Models.ConflictLineType.Ours : Models.ConflictLineType.Theirs);
                }

                return lines;
            }

            lines.Add(r.StartMarker);
            types.Add(Models.ConflictLineType.Marker);

            foreach (var l in r.OursContent)
            {
                lines.Add(l);
                types.Add(Models.ConflictLineType.Ours);
            }

            lines.Add(r.SeparatorMarker);
            types.Add(Models.ConflictLineType.Marker);

            foreach (var l in r.TheirsContent)
            {
                lines.Add(l);
                types.Add(Models.ConflictLineType.Theirs);
            }

            lines.Add(r.EndMarker);
            types.Add(Models.ConflictLineType.Marker);
            return lines;
        }

        private void ApplyRegion(int regionIdx)
        {
            var r = _conflictRegions[regionIdx];
            var range = _ranges[regionIdx];

            var builder = new StringBuilder();
            foreach (var line in ComposeRegion(r, range.LineTypes))
                builder.Append(line).Append('\n');

            // The last region of a file has no trailing line break of its own.
            if (r.IsAtEnd && builder.Length > 0)
                builder.Length--;

            var text = builder.ToString();
            var start = range.Start.Offset;

            _isApplying = true;
            ResultDocument.Replace(start, range.End.Offset - start, text);
            _isApplying = false;

            range.Start = CreateAnchor(start);
            range.End = CreateAnchor(start + text.Length);
        }

        private void AddAllPicks(Models.ConflictRegion r, Models.ConflictPanelType side)
        {
            var count = side == Models.ConflictPanelType.Ours ? r.OursRowCount : r.TheirsRowCount;
            for (var i = 0; i < count; i++)
                r.Picks.Add(new(side, i));
        }

        private TextAnchor CreateAnchor(int offset)
        {
            var anchor = ResultDocument.CreateAnchor(offset);
            anchor.MovementType = AnchorMovementType.BeforeInsertion;
            anchor.SurviveDeletion = true;
            return anchor;
        }

        private void OnResultDocumentChanging(object sender, DocumentChangeEventArgs e)
        {
            if (_isApplying)
                return;

            _touchedRegions.Clear();
            for (var i = 0; i < _ranges.Count; i++)
            {
                var start = _ranges[i].Start.Offset;
                var end = _ranges[i].End.Offset;
                var removed = e.RemovalLength > 0 && start < e.Offset + e.RemovalLength && e.Offset < end;
                var inserted = e.InsertionLength > 0 && start <= e.Offset && e.Offset < end;
                if (removed || inserted)
                    _touchedRegions.Add(i);
            }
        }

        private void OnResultDocumentChanged(object sender, DocumentChangeEventArgs e)
        {
            if (_isApplying)
                return;

            foreach (var i in _touchedRegions)
            {
                var r = _conflictRegions[i];
                r.IsCustom = true;
                r.HasChoice = false;
                r.Picks.Clear();
                _ranges[i].LineTypes.Clear();
            }

            _touchedRegions.Clear();
            OnRegionsChanged();
        }

        private void OnRegionsChanged()
        {
            IsDirty = true;
            RefreshRegionStates();
            RegionsChanged?.Invoke();
        }

        private void RefreshRegionStates()
        {
            var doc = ResultDocument;
            var decorations = new Models.ConflictLineDecoration[doc.LineCount];
            var unsolved = 0;

            for (var i = 0; i < _ranges.Count; i++)
            {
                var r = _conflictRegions[i];
                var range = _ranges[i];
                var start = range.Start.Offset;
                var end = range.End.Offset;

                range.FirstLine = doc.GetLineByOffset(start).LineNumber - 1;
                range.LineCount = end > start ? doc.GetLineByOffset(end - 1).LineNumber - range.FirstLine : 0;

                r.IsResolved = r.IsCustom ? !ContainsMarkers(range) : r.HasChoice;
                if (!r.IsResolved)
                    unsolved++;

                for (var k = 0; k < range.LineCount; k++)
                {
                    var type = !r.IsCustom && k < range.LineTypes.Count ? range.LineTypes[k] : Models.ConflictLineType.Edited;
                    decorations[range.FirstLine + k] = new(type, true, r.IsResolved, k == 0, k == range.LineCount - 1);
                }
            }

            _resultDecorations = decorations;
            UnsolvedCount = unsolved;
        }

        private bool ContainsMarkers(RegionRange range)
        {
            var doc = ResultDocument;
            for (var k = 0; k < range.LineCount; k++)
            {
                var text = doc.GetText(doc.GetLineByNumber(range.FirstLine + k + 1));
                if (text.StartsWith("<<<<<<<", StringComparison.Ordinal) ||
                    text.StartsWith("=======", StringComparison.Ordinal) ||
                    text.StartsWith(">>>>>>>", StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private class RegionRange
        {
            public TextAnchor Start { get; set; }
            public TextAnchor End { get; set; }
            public int FirstLine { get; set; }
            public int LineCount { get; set; }
            public List<Models.ConflictLineType> LineTypes { get; } = [];
        }

        private readonly Repository _repo;
        private readonly string _filePath;
        private int _unsolvedCount = 0;
        private int _maxLineNumber = 0;
        private int _currentConflictIndex = -1;
        private bool _isApplying = false;
        private List<Models.ConflictLine> _oursLines = [];
        private List<Models.ConflictLine> _theirsLines = [];
        private List<Models.ConflictRegion> _conflictRegions = [];
        private List<RegionRange> _ranges = [];
        private List<(string Common, int Region)> _layout = [];
        private List<int> _touchedRegions = [];
        private Models.ConflictLineDecoration[] _resultDecorations = [];
        private Models.ConflictSelectedChunk _selectedChunk;
        private string _error = string.Empty;
    }
}
