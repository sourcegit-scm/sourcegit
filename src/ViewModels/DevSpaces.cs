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

        public AvaloniaList<DevSpaceTerminal> Sessions { get; } = [];

        public AvaloniaList<DevSpaceGridSlot> VisibleSlots { get; } = [];

        public bool IsFilesActive
        {
            get => _isFilesActive;
            private set => SetProperty(ref _isFilesActive, value);
        }

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

        public int GridRows => Models.DevSpaceLayoutExtensions.GetRows(_layout, Sessions.Count);

        public int GridColumns => Models.DevSpaceLayoutExtensions.GetColumns(_layout, Sessions.Count);

        public int GridCapacity => GridRows * GridColumns;

        public DevSpaces(
            string workingDirectory,
            SourceGit.DevSpaces.IDevSpaceSessionLauncher launcher = null)
        {
            _workingDirectory = workingDirectory;
            Launcher = launcher ?? new SourceGit.DevSpaces.LocalDevSpaceSessionLauncher();
            Files = new DevSpaceFiles(workingDirectory);

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
            if (Sessions.Count == 0)
                CreateTerminal();
        }

        public void ActivateFiles()
        {
            IsFilesActive = true;
        }

        public DevSpaceTerminal CreateTerminal()
        {
            return CreateTerminalAt(-1);
        }

        public DevSpaceTerminal CreateTerminalAt(int preferredSlot)
        {
            var command = Preferences.Instance.DevSpacesDefaultCommand;
            return CreateTerminalAt(preferredSlot, command, GetTerminalDisplayName(command));
        }

        public DevSpaceTerminal CreateTerminalAt(int preferredSlot, string command, string displayName)
        {
            if (string.IsNullOrWhiteSpace(command))
                command = Preferences.Instance.DevSpacesDefaultCommand;
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = GetTerminalDisplayName(command);

            var number = _nextSessionNumber++;
            var terminal = new DevSpaceTerminal($"{displayName} {number}", command, _workingDirectory);

            Sessions.Add(terminal);
            ActiveTerminal = terminal;
            IsFilesActive = false;
            _preferredSlot = preferredSlot;
            RebuildSlots();
            return terminal;
        }

        public void ActivateTerminal(DevSpaceTerminal terminal)
        {
            if (terminal == null || !Sessions.Contains(terminal))
                return;

            ActiveTerminal = terminal;
            IsFilesActive = false;
            RebuildSlots();
        }

        public void CloseTerminal(DevSpaceTerminal terminal)
        {
            if (terminal == null || !Sessions.Remove(terminal))
                return;

            terminal.Dispose();
            if (ActiveTerminal == terminal)
                ActiveTerminal = Sessions.Count > 0 ? Sessions[Sessions.Count - 1] : null;

            RebuildSlots();
        }

        public void StopAll()
        {
            for (var i = Sessions.Count - 1; i >= 0; i--)
                Sessions[i].Dispose();

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
            StopAll();
        }

        private static string GetTerminalDisplayName(string command)
        {
            var normalized = command?.Trim().ToLowerInvariant();
            return normalized switch
            {
                "copilot" => "Copilot",
                "pwsh" or "__devspaces_pwsh__" => "PowerShell 7",
                "powershell" or "powershell.exe" or "__devspaces_powershell__" => "Windows PowerShell",
                "cmd" or "cmd.exe" or "__devspaces_cmd__" => "Command Prompt",
                "__devspaces_git_bash__" => "Git Bash",
                "__devspaces_shell__" => "Shell",
                _ => "Terminal",
            };
        }

        private void RebuildSlots()
        {
            var capacity = GridCapacity;
            var slots = new DevSpaceTerminal[capacity];

            if (capacity == 1)
            {
                slots[0] = ActiveTerminal ?? (Sessions.Count > 0 ? Sessions[0] : null);
            }
            else
            {
                var placeActiveInPreferredSlot =
                    ActiveTerminal != null &&
                    _preferredSlot >= 0 &&
                    _preferredSlot < capacity &&
                    Sessions.Contains(ActiveTerminal);

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

                    slots[slotIndex] = session;
                    slotIndex++;
                }

                if (ActiveTerminal != null && Array.IndexOf(slots, ActiveTerminal) < 0)
                    slots[capacity - 1] = ActiveTerminal;
            }

            VisibleSlots.Clear();
            for (var i = 0; i < capacity; i++)
                VisibleSlots.Add(new DevSpaceGridSlot(i, slots[i]));

            _preferredSlot = -1;
            OnPropertyChanged(nameof(GridRows));
            OnPropertyChanged(nameof(GridColumns));
            OnPropertyChanged(nameof(GridCapacity));
            OnPropertyChanged(nameof(VisibleSlots));
        }

        private readonly string _workingDirectory;
        private DevSpaceTerminal _activeTerminal;
        private Models.DevSpaceLayout _layout;
        private bool _isFilesActive;
        private int _nextSessionNumber = 1;
        private int _preferredSlot = -1;
    }
}
