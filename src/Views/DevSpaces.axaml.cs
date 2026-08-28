using System;
using System.Collections.Generic;
using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;

namespace SourceGit.Views
{
    public partial class DevSpaces : UserControl, IDisposable
    {
        private sealed record TerminalPaneHandle(Border Root, DevSpaceTerminal TerminalView);

        public DevSpaces()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        public void Dispose()
        {
            if (_owner != null)
                _owner.PropertyChanged -= OnOwnerPropertyChanged;

            DisposePanes();
            _owner = null;
            DataContext = null;
        }

        private void OnDataContextChanged(object sender, EventArgs e)
        {
            var next = DataContext as ViewModels.DevSpaces;
            if (ReferenceEquals(_owner, next))
                return;

            if (_owner != null)
                _owner.PropertyChanged -= OnOwnerPropertyChanged;

            DisposePanes();
            _owner = next;

            if (_owner != null)
                _owner.PropertyChanged += OnOwnerPropertyChanged;

            RebuildGrid();
        }

        private void OnOwnerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModels.DevSpaces.VisibleSlots) ||
                e.PropertyName == nameof(ViewModels.DevSpaces.GridRows) ||
                e.PropertyName == nameof(ViewModels.DevSpaces.GridColumns) ||
                e.PropertyName == nameof(ViewModels.DevSpaces.ActiveTerminal) ||
                e.PropertyName == nameof(ViewModels.DevSpaces.Layout))
            {
                Dispatcher.UIThread.Post(RebuildGrid);
            }
        }

        private void OnCreateTerminal(object sender, RoutedEventArgs e)
        {
            _owner?.CreateTerminal();
            e.Handled = true;
        }

        private void OnTerminalTabPressed(object sender, PointerPressedEventArgs e)
        {
            if (_owner != null && sender is Border { DataContext: ViewModels.DevSpaceTerminal session })
                _owner.ActivateTerminal(session);

            e.Handled = true;
        }

        private void OnCloseTerminal(object sender, RoutedEventArgs e)
        {
            if (_owner != null && sender is Button { DataContext: ViewModels.DevSpaceTerminal session })
                CloseTerminal(session);

            e.Handled = true;
        }

        private void CloseTerminal(ViewModels.DevSpaceTerminal session)
        {
            _owner?.CloseTerminal(session);
            if (_panes.Remove(session.Id, out var pane))
                pane.TerminalView.Dispose();

            RebuildGrid();
        }

        private void RebuildGrid()
        {
            TerminalGrid.Children.Clear();

            if (_owner == null)
            {
                TerminalGrid.Rows = 1;
                TerminalGrid.Columns = 1;
                return;
            }

            TerminalGrid.Rows = _owner.GridRows;
            TerminalGrid.Columns = _owner.GridColumns;

            foreach (var slot in _owner.VisibleSlots)
            {
                if (slot.Terminal != null)
                    TerminalGrid.Children.Add(GetOrCreatePane(slot.Terminal));
                else if (_owner.Layout == Models.DevSpaceLayout.Auto)
                    TerminalGrid.Children.Add(new Border { Margin = new Thickness(2) });
                else
                    TerminalGrid.Children.Add(CreateEmptySlot(slot.Index));
            }
        }

        private Border GetOrCreatePane(ViewModels.DevSpaceTerminal session)
        {
            if (_panes.TryGetValue(session.Id, out var cached))
                return cached.Root;

            var terminalView = new DevSpaceTerminal
            {
                DataContext = session,
            };

            var title = new TextBlock
            {
                Text = session.Title,
                Margin = new Thickness(8, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };

            var close = new Button
            {
                Width = 24,
                Height = 24,
                Margin = new Thickness(2),
                Content = "×",
                DataContext = session,
            };
            close.Classes.Add("icon_button");
            close.Click += (_, e) =>
            {
                CloseTerminal(session);
                e.Handled = true;
            };
            ToolTip.SetTip(close, App.Text("DevSpaces.CloseTerminal"));

            var header = new Grid
            {
                Height = 28,
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            };
            header.Children.Add(title);
            Grid.SetColumn(close, 1);
            header.Children.Add(close);
            header.PointerPressed += (_, e) =>
            {
                _owner?.ActivateTerminal(session);
                e.Handled = true;
            };

            var content = new Grid
            {
                RowDefinitions = new RowDefinitions("28,*"),
            };
            content.Children.Add(header);
            Grid.SetRow(terminalView, 1);
            content.Children.Add(terminalView);

            var root = new Border
            {
                Margin = new Thickness(2),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Child = content,
            };

            var handle = new TerminalPaneHandle(root, terminalView);
            _panes.Add(session.Id, handle);
            terminalView.Start(_owner.Launcher);
            return root;
        }

        private Button CreateEmptySlot(int slotIndex)
        {
            var button = new Button
            {
                Margin = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Content = App.Text("DevSpaces.NewTerminal"),
                Tag = slotIndex,
            };
            button.Classes.Add("flat");
            button.Click += (_, e) =>
            {
                if (_owner != null)
                    _owner.CreateTerminalAt(slotIndex);

                e.Handled = true;
            };
            return button;
        }

        private void DisposePanes()
        {
            foreach (var pane in _panes.Values)
                pane.TerminalView.Dispose();

            _panes.Clear();
            TerminalGrid.Children.Clear();
        }

        private readonly Dictionary<Guid, TerminalPaneHandle> _panes = [];
        private ViewModels.DevSpaces _owner;
    }
}
