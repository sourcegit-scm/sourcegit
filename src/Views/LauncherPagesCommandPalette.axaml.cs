using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public class LauncherPagesCommandPalettePageStatus : TextBlock
    {
        public static readonly DirectProperty<LauncherPagesCommandPalettePageStatus, Models.Branch> BranchProperty =
            AvaloniaProperty.RegisterDirect<LauncherPagesCommandPalettePageStatus, Models.Branch>(
                nameof(Branch),
                static o => o.Branch,
                static (o, v) => o.Branch = v);

        public Models.Branch Branch
        {
            get => _branch;
            set => SetAndRaise(BranchProperty, ref _branch, value);
        }

        protected override Type StyleKeyOverride => typeof(TextBlock);

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == BranchProperty)
            {
                if (_branch != null)
                {
                    var track = string.Empty;
                    if (_branch.Ahead.Count > 0)
                        track = _branch.Behind.Count > 0 ? $"{_branch.Ahead.Count}↑ {_branch.Behind.Count}↓" : $"{_branch.Ahead.Count}↑";
                    else if (_branch.Behind.Count > 0)
                        track = $"{_branch.Behind.Count}↓";

                    SetCurrentValue(TextProperty, track);
                    SetCurrentValue(IsVisibleProperty, !string.IsNullOrEmpty(track));
                }
                else
                {
                    SetCurrentValue(IsVisibleProperty, false);
                }
            }
        }

        private Models.Branch _branch = null;
    }

    public partial class LauncherPagesCommandPalette : UserControl
    {
        public LauncherPagesCommandPalette()
        {
            InitializeComponent();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            FilterTextBox.Focus(NavigationMethod.Directional);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (DataContext is not ViewModels.LauncherPagesCommandPalette vm)
                return;

            if (e.Key == Key.Enter)
            {
                vm.OpenOrSwitchTo();
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (RepoListBox.IsKeyboardFocusWithin)
                {
                    if (vm.VisiblePages.Count > 0)
                    {
                        PageListBox.Focus(NavigationMethod.Directional);
                        vm.SelectedPage = vm.VisiblePages[^1];
                    }
                    else
                    {
                        FilterTextBox.Focus(NavigationMethod.Directional);
                    }

                    e.Handled = true;
                    return;
                }

                if (PageListBox.IsKeyboardFocusWithin)
                {
                    FilterTextBox.Focus(NavigationMethod.Directional);
                    e.Handled = true;
                    return;
                }
            }
            else if (e.Key == Key.Down || e.Key == Key.Tab)
            {
                if (FilterTextBox.IsKeyboardFocusWithin)
                {
                    if (vm.VisiblePages.Count > 0)
                    {
                        PageListBox.Focus(NavigationMethod.Directional);
                        vm.SelectedPage = vm.VisiblePages[0];
                    }
                    else if (vm.VisibleRepos.Count > 0)
                    {
                        RepoListBox.Focus(NavigationMethod.Directional);
                        vm.SelectedRepo = vm.VisibleRepos[0];
                    }

                    e.Handled = true;
                    return;
                }

                if (PageListBox.IsKeyboardFocusWithin)
                {
                    if (vm.VisibleRepos.Count > 0)
                    {
                        RepoListBox.Focus(NavigationMethod.Directional);
                        vm.SelectedRepo = vm.VisibleRepos[0];
                    }
                    else if (e.Key == Key.Tab)
                    {
                        FilterTextBox.Focus(NavigationMethod.Directional);
                    }

                    e.Handled = true;
                    return;
                }

                if (RepoListBox.IsKeyboardFocusWithin && e.Key == Key.Tab)
                {
                    FilterTextBox.Focus(NavigationMethod.Directional);
                    e.Handled = true;
                    return;
                }
            }
        }

        private void OnItemTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.LauncherPagesCommandPalette vm)
            {
                vm.OpenOrSwitchTo();
                e.Handled = true;
            }
        }
    }
}
