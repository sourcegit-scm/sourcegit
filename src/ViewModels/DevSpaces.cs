using System;

using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public sealed class DevSpaceGridSlot
    {
        public int Index { get; }
        public DevSpaceTerminal Terminal { get; }

        public DevSpaceGridSlot(int index, DevSpaceTerminal terminal)
        {
            Index = index;
            Terminal = terminal;
        }
    }

    public sealed class DevSpaces : ObservableObject, IDisposable
    {
        public SourceGit.DevSpaces.IDevSpaceSessionLauncher Launcher { get; }
        public DevSpaceFiles Files { get; }
        public DevSpaceDashboard Dashboard { get; }
        public AvaloniaList<DevSpaceTerminal> Sessions { get; } = [];
        public AvaloniaList<DevSpaceGridSlot> VisibleSlots { get; } = [];

        public Models.DevSpacePage ActivePage
        {
            get => _activePage;
            private set
            {
                if (!SetProperty(ref _activePage, value))
                    return;
                OnPropertyChanged(nameof(IsDashboardActive));
                OnPropertyChanged(nameof(IsFilesActive));
                OnPropertyChanged(nameof(IsAIRouterActive));
                OnPropertyChanged(nameof(IsTerminalsActive));
                OnPropertyChanged(nameof(IsRoslynActive));
            }
        }

        public bool IsDashboardActive => ActivePage == Models.DevSpacePage.Dashboard;
        public bool IsFilesActive => ActivePage == Models.DevSpacePage.Files;
        public bool IsAIRouterActive => ActivePage == Models.DevSpacePage.AIRouter;
        public bool IsTerminalsActive => ActivePage == Models.DevSpacePage.Terminals;
        public bool IsRoslynActive => ActivePage == Models.DevSpacePage.Roslyn;

        public Models.DevSpaceTerminalDisplayMode TerminalDisplayMode
        {
            get => _terminalDisplayMode;
            set
            {
                if (!SetProperty(ref _terminalDisplayMode, value))
                    return;
                OnPropertyChanged(nameof(IsGridLayout));
                OnPropertyChanged(nameof(IsListLayout));
                RebuildSlots();
            }
        }

        public bool IsGridLayout => TerminalDisplayMode == Models.DevSpaceTerminalDisplayMode.Grid;
        public bool IsListLayout => TerminalDisplayMode == Models.DevSpaceTerminalDisplayMode.List;

        public DevSpaceTerminal ActiveTerminal
        {
            get => _activeTerminal;
            private set => SetProperty(ref _activeTerminal, value);
        }

        public Models.DevSpaceLayout Layout
        {
            get => _layout;
            set
            {
                if (value == Models.DevSpaceLayout.FourByFour)
                    value = Models.DevSpaceLayout.ThreeByThree;
                if (SetProperty(ref _layout, value))
                {
                    OnPropertyChanged(nameof(LayoutIndex));
                    RebuildSlots();
                }
            }
        }

        public int LayoutIndex
        {
            get => (int)_layout;
            set
            {
                if (value >= 0 && value <= 3)
                    Layout = (Models.DevSpaceLayout)value;
            }
        }

        public int GridRows => IsListLayout ? Math.Max(1, Sessions.Count) : Models.DevSpaceLayoutExtensions.GetRows(_layout, Sessions.Count);
        public int GridColumns => IsListLayout ? 1 : Models.DevSpaceLayoutExtensions.GetColumns(_layout, Sessions.Count);
        public int GridCapacity => IsListLayout ? Sessions.Count : GridRows * GridColumns;

        public DevSpaces(
            string workingDirectory,
            SourceGit.DevSpaces.IDevSpaceSessionLauncher launcher = null,
            SourceGit.DevSpaces.Terminal.DevSpaceTerminalRegistry terminalRegistry = null)
            : this(null, workingDirectory, launcher, terminalRegistry)
        {
        }

        public DevSpaces(
            Repository repository,
            string workingDirectory,
            SourceGit.DevSpaces.IDevSpaceSessionLauncher launcher = null,
            SourceGit.DevSpaces.Terminal.DevSpaceTerminalRegistry terminalRegistry = null)
        {
            _workingDirectory = workingDirectory;
            _terminalRegistry = terminalRegistry ?? SourceGit.DevSpaces.Terminal.DevSpaceTerminalRegistry.Instance;
            Launcher = launcher ?? new SourceGit.DevSpaces.LocalDevSpaceSessionLauncher();
            Files = new DevSpaceFiles(workingDirectory);
            Dashboard = new DevSpaceDashboard(this, workingDirectory, repository);

            var savedLayout = Preferences.Instance.DevSpacesDefaultLayout;
            if (savedLayout == Models.DevSpaceLayout.FourByFour)
            {
                savedLayout = Models.DevSpaceLayout.ThreeByThree;
                Preferences.Instance.DevSpacesDefaultLayout = savedLayout;
            }
            _layout = savedLayout;
            RebuildSlots();
        }

        public void EnsureFirstSession()
        {
            if (Sessions.Count != 0)
                return;

            var activePage = ActivePage;
            CreateTerminal();
            ActivePage = activePage;
        }

        public void ActivateDashboard() => ActivePage = Models.DevSpacePage.Dashboard;
        public void ActivateFiles() => ActivePage = Models.DevSpacePage.Files;
        public void ActivateAIRouter() => ActivePage = Models.DevSpacePage.AIRouter;
        public void ActivateTerminals() => ActivePage = Models.DevSpacePage.Terminals;
        public void ActivateRoslyn() => ActivePage = Models.DevSpacePage.Roslyn;

        public bool OpenFile(string relativePath)
        {
            ActivateFiles();
            var opened = Files.OpenFile(relativePath);
            if (opened)
                Dashboard.AddActivity(DevSpaceActivityKind.FileOpened, relativePath);
            return opened;
        }

        public DevSpaceTerminal CreateTerminal() => CreateTerminalAt(-1);

        public DevSpaceTerminal CreateTerminalAt(int preferredSlot)
        {
            var settings = SourceGit.DevSpaces.DevSpaceProfileSettings.Instance;
            return CreateTerminalAt(preferredSlot, settings.DefaultTerminal,
                SourceGit.DevSpaces.DevSpaceProfileSettings.GetTerminalDisplayName(settings.DefaultTerminal));
        }

        public DevSpaceTerminal CreateTerminalAt(int preferredSlot, string terminal, string displayName) =>
            CreateTerminalAt(preferredSlot, terminal, displayName, _workingDirectory, null);

        public DevSpaceTerminal CreateTerminalAt(int preferredSlot, string terminal, string displayName, string workingDirectory, string startupCommand)
        {
            var settings = SourceGit.DevSpaces.DevSpaceProfileSettings.Instance;
            if (string.IsNullOrWhiteSpace(terminal))
                terminal = settings.DefaultTerminal;
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = SourceGit.DevSpaces.DevSpaceProfileSettings.GetTerminalDisplayName(terminal);
            if (string.IsNullOrWhiteSpace(workingDirectory))
                workingDirectory = _workingDirectory;

            var number = _nextSessionNumber++;
            var created = new DevSpaceTerminal(
                $"{displayName} {number}",
                terminal,
                workingDirectory,
                startupCommand,
                _workingDirectory);
            _terminalRegistry.Register(created);
            Sessions.Add(created);
            ActiveTerminal = created;
            ActivateTerminals();
            Dashboard.AddActivity(DevSpaceActivityKind.SessionStarted, $"{created.Title} started");
            _preferredSlot = preferredSlot;
            RebuildSlots();
            return created;
        }

        public DevSpaceTerminal CreateProfileTerminalAt(
            int preferredSlot,
            SourceGit.DevSpaces.DevSpaceTerminalProfile profile,
            bool showProfileIcon = true)
        {
            SourceGit.DevSpaces.DevSpaceProfileSettings.ValidateProfile(profile);
            var settings = SourceGit.DevSpaces.DevSpaceProfileSettings.Instance;
            var workingDirectory = SourceGit.DevSpaces.DevSpaceProfileSettings.ResolveWorkingDirectory(_workingDirectory, profile.Path);

            if (string.Equals(profile.Command, "codex", StringComparison.OrdinalIgnoreCase))
                SourceGit.DevSpaces.CodexWorkspaceTrust.EnsureTrusted(workingDirectory);
            else if (string.Equals(profile.Command, "agy", StringComparison.OrdinalIgnoreCase))
                SourceGit.DevSpaces.AntigravityWorkspaceTrust.EnsureTrusted(workingDirectory);

            return CreateTerminalAt(
                preferredSlot,
                settings.DefaultTerminal,
                showProfileIcon ? profile.DisplayName : profile.Name,
                workingDirectory,
                profile.Command);
        }

        public DevSpaceTerminal CreateCopilotTerminalAt(int preferredSlot)
        {
            SourceGit.DevSpaces.CopilotWorkspaceTrust.EnsureTrusted(_workingDirectory);
            var settings = SourceGit.DevSpaces.DevSpaceProfileSettings.Instance;
            return CreateTerminalAt(preferredSlot, settings.DefaultTerminal, "Copilot", _workingDirectory, "copilot");
        }

        public DevSpaceTerminal CreateAgentTerminalAt(int preferredSlot, SourceGit.DevSpaces.DevSpaceAgent agent)
        {
            ArgumentNullException.ThrowIfNull(agent);
            if (string.Equals(agent.Command, "copilot", StringComparison.OrdinalIgnoreCase))
                return CreateCopilotTerminalAt(preferredSlot);
            if (string.Equals(agent.Command, "codex", StringComparison.OrdinalIgnoreCase))
                SourceGit.DevSpaces.CodexWorkspaceTrust.EnsureTrusted(_workingDirectory);
            else if (string.Equals(agent.Command, "agy", StringComparison.OrdinalIgnoreCase))
                SourceGit.DevSpaces.AntigravityWorkspaceTrust.EnsureTrusted(_workingDirectory);

            var settings = SourceGit.DevSpaces.DevSpaceProfileSettings.Instance;
            return CreateTerminalAt(preferredSlot, settings.DefaultTerminal, agent.Name, _workingDirectory, agent.Command);
        }

        public void ActivateTerminal(DevSpaceTerminal terminal)
        {
            if (terminal == null || !Sessions.Contains(terminal))
                return;
            ActiveTerminal = terminal;
            ActivateTerminals();
            RebuildSlots();
        }

        public void CloseTerminal(DevSpaceTerminal terminal)
        {
            if (terminal == null || !Sessions.Remove(terminal))
                return;
            _terminalRegistry.Unregister(terminal.Id);
            Dashboard.AddActivity(DevSpaceActivityKind.SessionClosed, $"{terminal.Title} closed");
            terminal.Dispose();
            if (ActiveTerminal == terminal)
                ActiveTerminal = Sessions.Count > 0 ? Sessions[Sessions.Count - 1] : null;
            RebuildSlots();
        }

        public void StopAll()
        {
            for (var i = Sessions.Count - 1; i >= 0; i--)
            {
                _terminalRegistry.Unregister(Sessions[i].Id);
                Sessions[i].Dispose();
            }
            Sessions.Clear();
            VisibleSlots.Clear();
            ActiveTerminal = null;
            _preferredSlot = -1;
            OnPropertyChanged(nameof(GridRows));
            OnPropertyChanged(nameof(GridColumns));
            OnPropertyChanged(nameof(GridCapacity));
            OnPropertyChanged(nameof(VisibleSlots));
        }

        public void Dispose()
        {
            Dashboard.Dispose();
            StopAll();
        }

        private void RebuildSlots()
        {
            if (IsListLayout)
            {
                VisibleSlots.Clear();
                for (var i = 0; i < Sessions.Count; i++)
                    VisibleSlots.Add(new DevSpaceGridSlot(i, Sessions[i]));
                _preferredSlot = -1;
                NotifyLayoutChanged();
                return;
            }

            var capacity = GridCapacity;
            var slots = new DevSpaceTerminal[capacity];
            if (capacity == 1)
            {
                slots[0] = ActiveTerminal ?? (Sessions.Count > 0 ? Sessions[0] : null);
            }
            else
            {
                var placeActiveInPreferredSlot = ActiveTerminal != null && _preferredSlot >= 0 && _preferredSlot < capacity && Sessions.Contains(ActiveTerminal);
                if (placeActiveInPreferredSlot)
                    slots[_preferredSlot] = ActiveTerminal;
                var slotIndex = 0;
                foreach (var session in Sessions)
                {
                    if (placeActiveInPreferredSlot && session == ActiveTerminal)
                        continue;
                    while (slotIndex < capacity && slots[slotIndex] != null)
                        slotIndex++;
                    if (slotIndex >= capacity)
                        break;
                    slots[slotIndex++] = session;
                }
                if (ActiveTerminal != null && Array.IndexOf(slots, ActiveTerminal) < 0)
                    slots[capacity - 1] = ActiveTerminal;
            }
            VisibleSlots.Clear();
            for (var i = 0; i < capacity; i++)
                VisibleSlots.Add(new DevSpaceGridSlot(i, slots[i]));
            _preferredSlot = -1;
            NotifyLayoutChanged();
        }

        private void NotifyLayoutChanged()
        {
            OnPropertyChanged(nameof(GridRows));
            OnPropertyChanged(nameof(GridColumns));
            OnPropertyChanged(nameof(GridCapacity));
            OnPropertyChanged(nameof(VisibleSlots));
        }

        private readonly string _workingDirectory;
        private readonly SourceGit.DevSpaces.Terminal.DevSpaceTerminalRegistry _terminalRegistry;
        private DevSpaceTerminal _activeTerminal;
        private Models.DevSpaceLayout _layout;
        private Models.DevSpacePage _activePage = Models.DevSpacePage.Dashboard;
        private Models.DevSpaceTerminalDisplayMode _terminalDisplayMode = Models.DevSpaceTerminalDisplayMode.Grid;
        private int _nextSessionNumber = 1;
        private int _preferredSlot = -1;
    }
}
