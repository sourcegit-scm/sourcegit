using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public sealed class DevSpaceDashboard : ObservableObject, IDisposable
    {
        public string WorkspacePath { get; }
        public string WorkspaceName { get; }

        public DevSpaceCapabilityState CopilotCapability { get; }
        public DevSpaceCapabilityState CodexCapability { get; }
        public DevSpaceCapabilityState AntigravityCapability { get; }
        public DevSpaceCapabilityState RoslynCapability { get; } = DevSpaceCapabilityState.Unavailable;

        public IReadOnlyList<SourceGit.DevSpaces.DevSpaceTerminalProfile> Profiles =>
            SourceGit.DevSpaces.DevSpaceProfileSettings.Instance.Profiles;

        public string CurrentBranch
        {
            get => _currentBranch;
            private set => SetProperty(ref _currentBranch, value ?? string.Empty);
        }

        public string BaseBranch
        {
            get => _baseBranch;
            private set => SetProperty(ref _baseBranch, value ?? string.Empty);
        }

        public int AheadCount
        {
            get => _aheadCount;
            private set => SetProperty(ref _aheadCount, value);
        }

        public int BehindCount
        {
            get => _behindCount;
            private set => SetProperty(ref _behindCount, value);
        }

        public DevSpaceGitSummary GitSummary
        {
            get => _gitSummary;
            private set => SetProperty(ref _gitSummary, value);
        }

        public AvaloniaList<DevSpaceActivityEntry> Activity { get; } = [];

        public IReadOnlyList<DevSpaceDashboardSessionRow> Sessions => _owner.Sessions
            .Select(x => new DevSpaceDashboardSessionRow(x, x.Title, x.State, x.WorkingDirectory))
            .ToArray();

        public DevSpaceDashboard(DevSpaces owner, string workspacePath, Repository repository = null)
        {
            _owner = owner;
            _repository = repository;
            WorkspacePath = workspacePath;
            WorkspaceName = GetWorkspaceName(workspacePath);
            CopilotCapability = SourceGit.DevSpaces.DevSpaceToolHealth.CheckCommand("copilot");
            CodexCapability = SourceGit.DevSpaces.DevSpaceToolHealth.CheckCommand("codex");
            AntigravityCapability = SourceGit.DevSpaces.DevSpaceToolHealth.CheckCommand("agy");
            _owner.Sessions.CollectionChanged += OnSessionsChanged;

            if (_repository != null)
            {
                _repository.PropertyChanged += OnRepositoryPropertyChanged;
                if (_repository.WorkingCopy != null)
                    _repository.WorkingCopy.PropertyChanged += OnWorkingCopyPropertyChanged;
                RefreshRepositorySummary();
            }
        }

        public static DevSpaceGitSummary BuildGitSummary(
            IEnumerable<Models.Change> staged,
            IEnumerable<Models.Change> unstaged)
        {
            var states = new Dictionary<string, Models.ChangeState>(StringComparer.Ordinal);
            var stagedPaths = new HashSet<string>(StringComparer.Ordinal);
            var unstagedPaths = new HashSet<string>(StringComparer.Ordinal);

            foreach (var change in staged ?? [])
            {
                if (string.IsNullOrEmpty(change.Path))
                    continue;
                stagedPaths.Add(change.Path);
                var state = change.Index != Models.ChangeState.None ? change.Index : change.WorkTree;
                states[change.Path] = state;
            }

            foreach (var change in unstaged ?? [])
            {
                if (string.IsNullOrEmpty(change.Path))
                    continue;
                unstagedPaths.Add(change.Path);
                var state = change.WorkTree != Models.ChangeState.None ? change.WorkTree : change.Index;
                if (state != Models.ChangeState.None || !states.ContainsKey(change.Path))
                    states[change.Path] = state;
            }

            var added = 0;
            var modified = 0;
            var deleted = 0;
            var renamed = 0;
            foreach (var state in states.Values)
            {
                switch (state)
                {
                    case Models.ChangeState.Added:
                    case Models.ChangeState.Untracked:
                        added++;
                        break;
                    case Models.ChangeState.Deleted:
                        deleted++;
                        break;
                    case Models.ChangeState.Renamed:
                        renamed++;
                        break;
                    case Models.ChangeState.None:
                        break;
                    default:
                        modified++;
                        break;
                }
            }

            return new DevSpaceGitSummary(
                states.Count,
                added,
                modified,
                deleted,
                renamed,
                stagedPaths.Count,
                unstagedPaths.Count);
        }

        public void AddActivity(DevSpaceActivityKind kind, string text, DateTimeOffset? at = null)
        {
            Activity.Insert(0, new DevSpaceActivityEntry(kind, text ?? string.Empty, at ?? DateTimeOffset.UtcNow));
            while (Activity.Count > 20)
                Activity.RemoveAt(Activity.Count - 1);
        }

        public void OpenSession(DevSpaceTerminal terminal) => _owner.ActivateTerminal(terminal);
        public void CloseSession(DevSpaceTerminal terminal) => _owner.CloseTerminal(terminal);
        public void OpenFiles() => _owner.ActivateFiles();
        public void OpenWorkspaceFolder()
        {
            if (!string.IsNullOrWhiteSpace(WorkspacePath))
                Native.OS.OpenInFileManager(WorkspacePath);
        }

        public void OpenWorkingCopy()
        {
            if (_repository != null)
                _repository.SelectedViewIndex = 1;
        }

        public DevSpaceTerminal StartDefaultTerminal() => _owner.CreateTerminal();
        public DevSpaceTerminal StartProfile(SourceGit.DevSpaces.DevSpaceTerminalProfile profile) => _owner.CreateProfileTerminalAt(-1, profile);
        public DevSpaceTerminal StartAgent(SourceGit.DevSpaces.DevSpaceAgent agent) => _owner.CreateAgentTerminalAt(-1, agent);
        public void CloseAllSessions() => _owner.StopAll();

        public void Dispose()
        {
            _owner.Sessions.CollectionChanged -= OnSessionsChanged;
            if (_repository != null)
            {
                _repository.PropertyChanged -= OnRepositoryPropertyChanged;
                if (_repository.WorkingCopy != null)
                    _repository.WorkingCopy.PropertyChanged -= OnWorkingCopyPropertyChanged;
            }
        }

        private void OnSessionsChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
            OnPropertyChanged(nameof(Sessions));

        private void OnRepositoryPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Repository.CurrentBranch) ||
                e.PropertyName == nameof(Repository.LocalChangesCount))
                RefreshRepositorySummary();
        }

        private void OnWorkingCopyPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WorkingCopy.Staged) || e.PropertyName == nameof(WorkingCopy.Unstaged))
                RefreshRepositorySummary();
        }

        private void RefreshRepositorySummary()
        {
            if (_repository == null)
                return;

            var branch = _repository.CurrentBranch;
            CurrentBranch = branch?.Name ?? string.Empty;
            AheadCount = branch?.Ahead?.Count ?? 0;
            BehindCount = branch?.Behind?.Count ?? 0;
            BaseBranch = Models.WorktreeBaseBranch.ReadPersisted(_repository.GitDir, CurrentBranch);
            GitSummary = BuildGitSummary(_repository.WorkingCopy?.Staged, _repository.WorkingCopy?.Unstaged);
        }

        private static string GetWorkspaceName(string workspacePath)
        {
            if (string.IsNullOrWhiteSpace(workspacePath))
                return string.Empty;
            var trimmed = workspacePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.GetFileName(trimmed);
        }

        private readonly DevSpaces _owner;
        private readonly Repository _repository;
        private string _currentBranch = string.Empty;
        private string _baseBranch = string.Empty;
        private int _aheadCount;
        private int _behindCount;
        private DevSpaceGitSummary _gitSummary = DevSpaceGitSummary.Empty;
    }
}
