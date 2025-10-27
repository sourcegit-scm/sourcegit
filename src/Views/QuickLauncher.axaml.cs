using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class QuickLauncher : UserControl
    {
        public QuickLauncher()
        {
            InitializeComponent();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (DataContext is not ViewModels.QuickLauncher switcher)
                return;

            if (e.Key == Key.Enter)
            {
                switcher.OpenOrSwitchTo();
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (RepoListBox.IsKeyboardFocusWithin)
                {
                    if (switcher.VisiblePages.Count > 0)
                    {
                        PageListBox.Focus(NavigationMethod.Directional);
                        switcher.SelectedPage = switcher.VisiblePages[^1];
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
            else if (e.Key == Key.Down)
            {
                if (FilterTextBox.IsKeyboardFocusWithin)
                {
                    if (switcher.VisiblePages.Count > 0)
                    {
                        PageListBox.Focus(NavigationMethod.Directional);
                        switcher.SelectedPage = switcher.VisiblePages[0];
                    }
                    else if (switcher.VisibleRepos.Count > 0)
                    {
                        RepoListBox.Focus(NavigationMethod.Directional);
                        switcher.SelectedRepo = switcher.VisibleRepos[0];
                    }

                    e.Handled = true;
                    return;
                }

                if (PageListBox.IsKeyboardFocusWithin)
                {
                    if (switcher.VisibleRepos.Count > 0)
                    {
                        RepoListBox.Focus(NavigationMethod.Directional);
                        switcher.SelectedRepo = switcher.VisibleRepos[0];
                    }

                    e.Handled = true;
                    return;
                }
            }
        }

        private void OnItemTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.QuickLauncher switcher)
            {
                switcher.OpenOrSwitchTo();
                e.Handled = true;
            }
        }
    }
}
