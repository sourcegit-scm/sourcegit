using System;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class LocalChangesDetail : UserControl
    {
        public static readonly DirectProperty<LocalChangesDetail, bool> IsDetailsPanelExpandedProperty =
            AvaloniaProperty.RegisterDirect<LocalChangesDetail, bool>(
                nameof(IsDetailsPanelExpanded),
                static o => o.IsDetailsPanelExpanded,
                static (o, v) => o.IsDetailsPanelExpanded = v);

        public bool IsDetailsPanelExpanded
        {
            get => _isDetailsPanelExpanded;
            set => SetAndRaise(IsDetailsPanelExpandedProperty, ref _isDetailsPanelExpanded, value);
        }

        public LocalChangesDetail()
        {
            InitializeComponent();
        }

        private void OnTabHeaderPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                e.Handled = true;
        }

        private void OnChangeActivated(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.LocalChangesDetail vm)
                vm.PresentMainDiff();
        }

        private async void OnChangeCollectionViewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not ViewModels.LocalChangesDetail vm)
                return;

            if (sender is not ChangeCollectionView { Selection: { Count: > 0 } selection })
                return;

            var cmdKey = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
            if (e.Key == Key.C && e.KeyModifiers.HasFlag(cmdKey))
            {
                var builder = new StringBuilder();
                var copyAbsPath = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
                if (selection.IsSingleFolder)
                    builder.Append(copyAbsPath ? vm.GetAbsPath(selection.SingleFolderPath) : selection.SingleFolderPath);
                else if (selection.Changes.Count == 1)
                    builder.Append(copyAbsPath ? vm.GetAbsPath(selection.Changes[0].Path) : selection.Changes[0].Path);
                else
                {
                    foreach (var c in selection.Changes)
                        builder.AppendLine(copyAbsPath ? vm.GetAbsPath(c.Path) : c.Path);
                }

                await this.CopyTextAsync(builder.ToString());
                e.Handled = true;
            }
            else if (e.Key == Key.F && e.KeyModifiers == cmdKey)
            {
                ChangeSearchBox.Focus();
                e.Handled = true;
            }
        }

        private bool _isDetailsPanelExpanded = true;
    }
}
