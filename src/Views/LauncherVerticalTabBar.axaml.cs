using Avalonia.Controls;

namespace SourceGit.Views
{
    public partial class LauncherVerticalTabBar : LauncherTabBarBase
    {
        protected override string CloseFollowingTabsTextKey => "PageTabBar.Tab.CloseBelow";

        public LauncherVerticalTabBar()
        {
            InitializeComponent();
        }

        private void OnTabsSelectionChanged(object sender, SelectionChangedEventArgs _)
        {
            if (sender is ListBox { SelectedItem: { } selected } list)
                list.ScrollIntoView(selected);
        }
    }
}
