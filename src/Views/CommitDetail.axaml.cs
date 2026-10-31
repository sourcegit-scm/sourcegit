using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public partial class CommitDetail : UserControl
    {
        public static readonly DirectProperty<CommitDetail, bool> IsDetailsPanelExpandedProperty =
            AvaloniaProperty.RegisterDirect<CommitDetail, bool>(
                nameof(IsDetailsPanelExpanded),
                static o => o.IsDetailsPanelExpanded,
                static (o, v) => o.IsDetailsPanelExpanded = v);

        public bool IsDetailsPanelExpanded
        {
            get => _isDetailsPanelExpanded;
            set => SetAndRaise(IsDetailsPanelExpandedProperty, ref _isDetailsPanelExpanded, value);
        }

        public CommitDetail()
        {
            InitializeComponent();
        }

        public ContextMenu CreateChangeContextMenu(Models.Change change)
        {
            return CommitInformation.CreateChangeContextMenu(this, change);
        }

        private void OnTabHeaderPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (ViewModels.Preferences.Instance.UseTwoColumnsLayoutInHistories)
                return;

            var historiesView = this.FindAncestorOfType<Histories>();
            if (historiesView is not { DataContext: ViewModels.Histories vm })
                return;

            if (vm.IsCollapseDetails)
                vm.IsCollapseDetails = false;
        }

        private bool _isDetailsPanelExpanded = true;
    }
}
