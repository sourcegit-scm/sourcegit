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
        public static readonly DirectProperty<BranchSelectorChoice, Models.Branch> BranchProperty =
            AvaloniaProperty.RegisterDirect<BranchSelectorChoice, Models.Branch>(
                nameof(Branch),
                static o => o.Branch,
                static (o, v) => o.Branch = v);

        public Models.Branch Branch
        {
            get => _branch;
            set => SetAndRaise(BranchProperty, ref _branch, value);
        }

        public static readonly DirectProperty<BranchSelectorChoice, bool> UsePureNameProperty =
            AvaloniaProperty.RegisterDirect<BranchSelectorChoice, bool>(
                nameof(UsePureName),
                static o => o.UsePureName,
                static (o, v) => o.UsePureName = v);

        public bool UsePureName
        {
            get => _usePureName;
            set => SetAndRaise(UsePureNameProperty, ref _usePureName, value);
        }

        protected override Type StyleKeyOverride => typeof(TextBlock);

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == BranchProperty || change.Property == UsePureNameProperty)
            {
                if (_branch != null)
                    Text = _usePureName ? _branch.Name : _branch.FriendlyName;
                else
                    Text = "---";
            }
        }

        private Models.Branch _branch = null;
        private bool _usePureName = false;
    }

    public partial class BranchSelector : UserControl
    {
        public static readonly DirectProperty<BranchSelector, List<Models.Branch>> BranchesProperty =
            AvaloniaProperty.RegisterDirect<BranchSelector, List<Models.Branch>>(
                nameof(Branches),
                static o => o.Branches,
                static (o, v) => o.Branches = v);

        public List<Models.Branch> Branches
        {
            get => _branches;
            set => SetAndRaise(BranchesProperty, ref _branches, value);
        }

        public static readonly DirectProperty<BranchSelector, List<Models.Branch>> VisibleBranchesProperty =
            AvaloniaProperty.RegisterDirect<BranchSelector, List<Models.Branch>>(
                nameof(VisibleBranches),
                static o => o.VisibleBranches);

        public List<Models.Branch> VisibleBranches
        {
            get => _visibleBranches;
            set => SetAndRaise(VisibleBranchesProperty, ref _visibleBranches, value);
        }

        public static readonly DirectProperty<BranchSelector, Models.Branch> SelectedBranchProperty =
            AvaloniaProperty.RegisterDirect<BranchSelector, Models.Branch>(
                nameof(SelectedBranch),
                static o => o.SelectedBranch,
                static (o, v) => o.SelectedBranch = v);

        public Models.Branch SelectedBranch
        {
            get => _selectedBranch;
            set => SetAndRaise(SelectedBranchProperty, ref _selectedBranch, value);
        }

        public static readonly DirectProperty<BranchSelector, bool> IsDropDownOpenedProperty =
            AvaloniaProperty.RegisterDirect<BranchSelector, bool>(
                nameof(IsDropDownOpened),
                static o => o.IsDropDownOpened,
                static (o, v) => o.IsDropDownOpened = v);

        public bool IsDropDownOpened
        {
            get => _isDropDownOpened;
            set => SetAndRaise(IsDropDownOpenedProperty, ref _isDropDownOpened, value);
        }

        public static readonly DirectProperty<BranchSelector, string> SearchFilterProperty =
            AvaloniaProperty.RegisterDirect<BranchSelector, string>(
                nameof(SearchFilter),
                static o => o.SearchFilter,
                static (o, v) => o.SearchFilter = v);

        public string SearchFilter
        {
            get => _searchFilter;
            set => SetAndRaise(SearchFilterProperty, ref _searchFilter, value);
        }

        public static readonly DirectProperty<BranchSelector, bool> UsePureNameProperty =
            AvaloniaProperty.RegisterDirect<BranchSelector, bool>(
                nameof(UsePureName),
                static o => o.UsePureName,
                static (o, v) => o.UsePureName = v);

        public bool UsePureName
        {
            get => _usePureName;
            set => SetAndRaise(UsePureNameProperty, ref _usePureName, value);
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
                if (_branches is not { Count: > 0 })
                {
                    VisibleBranches = [];
                }
                else if (string.IsNullOrEmpty(_searchFilter))
                {
                    VisibleBranches = _branches;
                }
                else
                {
                    var visible = new List<Models.Branch>();
                    var oldSelection = _selectedBranch;
                    var keepSelection = false;

                    foreach (var b in _branches)
                    {
                        if (b.FriendlyName.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                        {
                            visible.Add(b);
                            if (!keepSelection)
                                keepSelection = (b == oldSelection);
                        }
                    }

                    VisibleBranches = visible;
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

        private List<Models.Branch> _branches;
        private List<Models.Branch> _visibleBranches;
        private bool _isDropDownOpened;
        private string _searchFilter = string.Empty;
        private bool _usePureName = false;
        private Popup _popup = null;
        private Models.Branch _selectedBranch = null;
    }
}
