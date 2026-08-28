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

        public AvaloniaList<DevSpaceTerminal> Sessions { get; } = [];

        public AvaloniaList<DevSpaceGridSlot> VisibleSlots { get; } = [];

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
                if (value >= 0 && value <= 4)
                    Layout = (Models.DevSpaceLayout)value;
            }
        }

        public int GridDimension => _layout.GetDimension(Sessions.Count);

        public DevSpaces(
            string workingDirectory,
            SourceGit.DevSpaces.IDevSpaceSessionLauncher launcher = null)
        {
            _workingDirectory = workingDirectory;
            Launcher = launcher ?? new SourceGit.DevSpaces.LocalDevSpaceSessionLauncher();
            _layout = Preferences.Instance.DevSpacesDefaultLayout;
            RebuildSlots();
        }

        public void EnsureFirstSession()
        {
            if (Sessions.Count == 0)
                CreateTerminal();
        }

        public DevSpaceTerminal CreateTerminal()
        {
            return CreateTerminalAt(-1);
        }

        public DevSpaceTerminal CreateTerminalAt(int preferredSlot)
        {
            var command = Preferences.Instance.DevSpacesDefaultCommand;
            var number = _nextSessionNumber++;
            var prefix = command.Trim().Equals("copilot", StringComparison.OrdinalIgnoreCase)
                ? "Copilot"
                : "Terminal";
            var terminal = new DevSpaceTerminal($"{prefix} {number}", command, _workingDirectory);

            Sessions.Add(terminal);
            ActiveTerminal = terminal;
            _preferredSlot = preferredSlot;
            RebuildSlots();
            return terminal;
        }

        public void ActivateTerminal(DevSpaceTerminal terminal)
        {
            if (terminal == null || !Sessions.Contains(terminal))
                return;

            ActiveTerminal = terminal;
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
            OnPropertyChanged(nameof(GridDimension));
            OnPropertyChanged(nameof(VisibleSlots));
        }

        public void Dispose()
        {
            StopAll();
        }

        private void RebuildSlots()
        {
            var dimension = GridDimension;
            var capacity = dimension * dimension;
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
            OnPropertyChanged(nameof(GridDimension));
            OnPropertyChanged(nameof(VisibleSlots));
        }

        private readonly string _workingDirectory;
        private DevSpaceTerminal _activeTerminal;
        private Models.DevSpaceLayout _layout;
        private int _nextSessionNumber = 1;
        private int _preferredSlot = -1;
    }
}
