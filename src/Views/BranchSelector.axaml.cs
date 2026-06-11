using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public class BranchSelectorChoice : TextBlock
    {
        public static readonly StyledProperty<Models.Branch> BranchProperty =
            AvaloniaProperty.Register<BranchSelectorChoice, Models.Branch>(nameof(Branch));

        public Models.Branch Branch
        {
            get => GetValue(BranchProperty);
            set => SetValue(BranchProperty, value);
        }

        public static readonly StyledProperty<bool> UsePureNameProperty =
            AvaloniaProperty.Register<BranchSelectorChoice, bool>(nameof(UsePureName));

        public bool UsePureName
        {
            get => GetValue(UsePureNameProperty);
            set => SetValue(UsePureNameProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(TextBlock);

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == BranchProperty || change.Property == UsePureNameProperty)
            {
                if (Branch is { } branch)
                    Text = UsePureName ? branch.Name : branch.FriendlyName;
                else
                    Text = "---";
            }
        }
    }

    public partial class BranchSelector : UserControl
    {
        public static readonly StyledProperty<List<Models.Branch>> BranchesProperty =
            AvaloniaProperty.Register<BranchSelector, List<Models.Branch>>(nameof(Branches));

        public List<Models.Branch> Branches
        {
            get => GetValue(BranchesProperty);
            set => SetValue(BranchesProperty, value);
        }

        public static readonly StyledProperty<List<Models.Branch>> VisibleBranchesProperty =
            AvaloniaProperty.Register<BranchSelector, List<Models.Branch>>(nameof(VisibleBranches));

        public List<Models.Branch> VisibleBranches
        {
            get => GetValue(VisibleBranchesProperty);
            set => SetValue(VisibleBranchesProperty, value);
        }

        public static readonly DirectProperty<BranchSelector, Models.Branch> SelectedBranchProperty =
            AvaloniaProperty.RegisterDirect<BranchSelector, Models.Branch>(
                nameof(SelectedBranch),
                o => o.SelectedBranch,
                (o, v) => o.SelectedBranch = v);

        public Models.Branch SelectedBranch
        {
            get => _selectedBranch;
            set => SetAndRaise(SelectedBranchProperty, ref _selectedBranch, value);
        }

        public static readonly StyledProperty<bool> IsDropDownOpenedProperty =
            AvaloniaProperty.Register<BranchSelector, bool>(nameof(IsDropDownOpened));

        public bool IsDropDownOpened
        {
            get => GetValue(IsDropDownOpenedProperty);
            set => SetValue(IsDropDownOpenedProperty, value);
        }

        public static readonly StyledProperty<string> SearchFilterProperty =
            AvaloniaProperty.Register<BranchSelector, string>(nameof(SearchFilter));

        public string SearchFilter
        {
            get => GetValue(SearchFilterProperty);
            set => SetValue(SearchFilterProperty, value);
        }

        public static readonly StyledProperty<bool> UsePureNameProperty =
            AvaloniaProperty.Register<BranchSelector, bool>(nameof(UsePureName));

        public bool UsePureName
        {
            get => GetValue(UsePureNameProperty);
            set => SetValue(UsePureNameProperty, value);
        }

        public BranchSelector()
        {
            Focusable = true;
            InitializeComponent();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == BranchesProperty || change.Property == SearchFilterProperty)
            {
                var branches = Branches;
                var filter = SearchFilter;
                if (branches is not { Count: > 0 })
                {
                    SetCurrentValue(VisibleBranchesProperty, []);
                }
                else if (string.IsNullOrEmpty(filter))
                {
                    SetCurrentValue(VisibleBranchesProperty, Branches);
                }
                else
                {
                    var visible = new List<Models.Branch>();
                    var oldSelection = SelectedBranch;
                    var keepSelection = false;

                    foreach (var b in Branches)
                    {
                        if (b.FriendlyName.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase))
                        {
                            visible.Add(b);
                            if (!keepSelection)
                                keepSelection = (b == oldSelection);
                        }
                    }

                    SetCurrentValue(VisibleBranchesProperty, visible);
                    if (!keepSelection && visible.Count > 0)
                        SelectedBranch = visible[0];
                }
            }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            if (_popup != null)
            {
                _popup.Opened -= OnPopupOpened;
                _popup.Closed -= OnPopupClosed;
            }

            _popup = e.NameScope.Get<Popup>("PART_Popup");
            _popup.Opened += OnPopupOpened;
            _popup.Closed += OnPopupClosed;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.Space && !IsDropDownOpened)
            {
                IsDropDownOpened = true;
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && IsDropDownOpened)
            {
                IsDropDownOpened = false;
                e.Handled = true;
            }
        }

        private void OnPopupOpened(object sender, EventArgs e)
        {
            var listBox = _popup?.Child?.FindDescendantOfType<ListBox>();
            listBox?.Focus();
        }

        private void OnPopupClosed(object sender, EventArgs e)
        {
            Focus(NavigationMethod.Directional);
        }

        private void OnToggleDropDown(object sender, PointerPressedEventArgs e)
        {
            IsDropDownOpened = !IsDropDownOpened;
            e.Handled = true;
        }

        private void OnSearchBoxKeyDown(object _, KeyEventArgs e)
        {
            if (e.Key == Key.Tab)
            {
                var listBox = _popup?.Child?.FindDescendantOfType<ListBox>();
                listBox?.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                var listBox = _popup?.Child?.FindDescendantOfType<ListBox>();
                if (listBox != null)
                {
                    if (listBox.SelectedIndex > 0)
                        listBox.SelectedIndex--;
                    listBox.Focus();
                }

                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                var listBox = _popup?.Child?.FindDescendantOfType<ListBox>();
                if (listBox != null)
                {
                    if (listBox.SelectedIndex < listBox.Items.Count - 1)
                        listBox.SelectedIndex++;
                    listBox.Focus();
                }

                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                IsDropDownOpened = false;
                e.Handled = true;
            }
        }

        private void OnClearSearchFilter(object sender, RoutedEventArgs e)
        {
            SearchFilter = string.Empty;
            e.Handled = true;
        }

        private void OnDropDownListKeyDown(object _, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                IsDropDownOpened = false;
                e.Handled = true;
            }
            else if (e.Key == Key.F && e.KeyModifiers == (OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control))
            {
                var searchBox = _popup?.Child?.FindDescendantOfType<TextBox>();
                if (searchBox != null)
                {
                    searchBox.CaretIndex = SearchFilter?.Length ?? 0;
                    searchBox.Focus();
                }

                e.Handled = true;
            }
        }

        private void OnDropDownListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 1 && e.AddedItems[0] is Models.Branch branch)
                SelectedBranch = branch;
        }

        private void OnDropDownItemPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is Control { DataContext: Models.Branch branch })
                SelectedBranch = branch;

            IsDropDownOpened = false;
            e.Handled = true;
        }

        private Popup _popup = null;
        private Models.Branch _selectedBranch = null;
    }
}
