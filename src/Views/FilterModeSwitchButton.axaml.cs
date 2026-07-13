using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public partial class FilterModeSwitchButton : UserControl
    {
        public static readonly DirectProperty<FilterModeSwitchButton, Models.FilterMode> ModeProperty =
            AvaloniaProperty.RegisterDirect<FilterModeSwitchButton, Models.FilterMode>(
                nameof(Mode),
                static o => o.Mode,
                static (o, v) => o.Mode = v);

        public Models.FilterMode Mode
        {
            get => _mode;
            set => SetAndRaise(ModeProperty, ref _mode, value);
        }

        public static readonly DirectProperty<FilterModeSwitchButton, bool> IsContextMenuOpeningProperty =
            AvaloniaProperty.RegisterDirect<FilterModeSwitchButton, bool>(
                nameof(IsContextMenuOpening),
                static o => o.IsContextMenuOpening);

        public bool IsContextMenuOpening
        {
            get => _isContextMenuOpening;
            set => SetAndRaise(IsContextMenuOpeningProperty, ref _isContextMenuOpening, value);
        }

        public static readonly StyledProperty<bool> IsHoverParentProperty =
            AvaloniaProperty.Register<FilterModeSwitchButton, bool>(nameof(IsHoverParent));

        public bool IsHoverParent
        {
            get => GetValue(IsHoverParentProperty);
            set => SetValue(IsHoverParentProperty, value);
        }

        public FilterModeSwitchButton()
        {
            IsVisible = false;
            InitializeComponent();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ModeProperty ||
                change.Property == IsHoverParentProperty ||
                change.Property == IsContextMenuOpeningProperty)
            {
                var visible = (Mode != Models.FilterMode.None || IsHoverParent || IsContextMenuOpening);
                SetCurrentValue(IsVisibleProperty, visible);
            }
        }

        private void OnChangeFilterModeButtonClicked(object sender, RoutedEventArgs e)
        {
            var repoView = this.FindAncestorOfType<Repository>();

            if (repoView?.DataContext is not ViewModels.Repository repo)
                return;

            if (sender is not Button button)
                return;

            var menu = new ContextMenu();
            if (DataContext is ViewModels.TagListItem tagItem)
                FillContextMenuForTag(menu, repo, tagItem.Tag, tagItem.FilterMode);
            else if (DataContext is ViewModels.TagTreeNode tagNode)
                FillContextMenuForTag(menu, repo, tagNode.Tag, tagNode.FilterMode);
            else if (DataContext is ViewModels.BranchTreeNode branchNode)
                FillContextMenuForBranch(menu, repo, branchNode, branchNode.FilterMode);

            menu.Closed += (_, _) => IsContextMenuOpening = false;
            menu.Open(button);

            IsContextMenuOpening = true;
            e.Handled = true;
        }

        private void FillContextMenuForTag(ContextMenu menu, ViewModels.Repository repo, Models.Tag tag, Models.FilterMode current)
        {
            if (current != Models.FilterMode.None)
            {
                var unset = new MenuItem();
                unset.Header = App.Text("Repository.FilterCommits.Default");
                unset.Click += (_, ev) =>
                {
                    repo.SetTagFilterMode(tag, Models.FilterMode.None);
                    ev.Handled = true;
                };

                menu.Items.Add(unset);
                menu.Items.Add(new MenuItem() { Header = "-" });
            }

            var include = new MenuItem();
            include.Icon = this.CreateMenuIcon("Icons.Filter");
            include.Header = App.Text("Repository.FilterCommits.Include");
            include.IsEnabled = current != Models.FilterMode.Included;
            include.Click += (_, ev) =>
            {
                repo.SetTagFilterMode(tag, Models.FilterMode.Included);
                ev.Handled = true;
            };

            var exclude = new MenuItem();
            exclude.Icon = this.CreateMenuIcon("Icons.EyeClose");
            exclude.Header = App.Text("Repository.FilterCommits.Exclude");
            exclude.IsEnabled = current != Models.FilterMode.Excluded;
            exclude.Click += (_, ev) =>
            {
                repo.SetTagFilterMode(tag, Models.FilterMode.Excluded);
                ev.Handled = true;
            };

            menu.Items.Add(include);
            menu.Items.Add(exclude);
        }

        private void FillContextMenuForBranch(ContextMenu menu, ViewModels.Repository repo, ViewModels.BranchTreeNode node, Models.FilterMode current)
        {
            if (current != Models.FilterMode.None)
            {
                var unset = new MenuItem();
                unset.Header = App.Text("Repository.FilterCommits.Default");
                unset.Click += (_, ev) =>
                {
                    repo.SetBranchFilterMode(node, Models.FilterMode.None, false, true);
                    ev.Handled = true;
                };

                menu.Items.Add(unset);
                menu.Items.Add(new MenuItem() { Header = "-" });
            }

            var include = new MenuItem();
            include.Icon = this.CreateMenuIcon("Icons.Filter");
            include.Header = App.Text("Repository.FilterCommits.Include");
            include.IsEnabled = current != Models.FilterMode.Included;
            include.Click += (_, ev) =>
            {
                repo.SetBranchFilterMode(node, Models.FilterMode.Included, false, true);
                ev.Handled = true;
            };

            var exclude = new MenuItem();
            exclude.Icon = this.CreateMenuIcon("Icons.EyeClose");
            exclude.Header = App.Text("Repository.FilterCommits.Exclude");
            exclude.IsEnabled = current != Models.FilterMode.Excluded;
            exclude.Click += (_, ev) =>
            {
                repo.SetBranchFilterMode(node, Models.FilterMode.Excluded, false, true);
                ev.Handled = true;
            };

            menu.Items.Add(include);
            menu.Items.Add(exclude);
        }

        private Models.FilterMode _mode = Models.FilterMode.None;
        private bool _isContextMenuOpening = false;
    }
}
