using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class LauncherPageSwitcher : ObservableObject, IDisposable
    {
        public List<LauncherPage> VisiblePages
        {
            get => _visiblePages;
            private set => SetProperty(ref _visiblePages, value);
        }

        public string SearchFilter
        {
            get => _searchFilter;
            set
            {
                if (SetProperty(ref _searchFilter, value))
                    UpdateVisiblePages();
            }
        }

        public LauncherPage SelectedPage
        {
            get => _selectedPage;
            set => SetProperty(ref _selectedPage, value);
        }

        public LauncherPageSwitcher(Launcher launcher)
        {
            _launcher = launcher;
            UpdateVisiblePages();
            SelectedPage = launcher.ActivePage;
        }

        public void ClearFilter()
        {
            SearchFilter = string.Empty;
        }

        public void Switch()
        {
            _launcher.ActivePage = _selectedPage ?? _launcher.ActivePage;
            _launcher.CancelSwitcher();
        }

        public void Dispose()
        {
            _visiblePages.Clear();
            _selectedPage = null;
            _searchFilter = string.Empty;
        }

        private void UpdateVisiblePages()
        {
            var visible = new List<LauncherPage>();
            if (string.IsNullOrEmpty(_searchFilter))
            {
                visible.AddRange(_launcher.Pages);
            }
            else
            {
                foreach (var page in _launcher.Pages)
                {
                    if (page.Node.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                        (page.Node.IsRepository && page.Node.Id.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase)))
                    {
                        visible.Add(page);
                    }
                }
            }

            VisiblePages = visible;
            SelectedPage = visible.Count > 0 ? visible[0] : null;
        }

        private Launcher _launcher = null;
        private List<LauncherPage> _visiblePages = [];
        private string _searchFilter = string.Empty;
        private LauncherPage _selectedPage = null;
    }
}
