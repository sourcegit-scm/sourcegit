using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class QuickLauncher : ObservableObject
    {
        public List<LauncherPage> VisiblePages
        {
            get => _visiblePages;
            private set => SetProperty(ref _visiblePages, value);
        }

        public List<RepositoryNode> VisibleRepos
        {
            get => _visibleRepos;
            private set => SetProperty(ref _visibleRepos, value);
        }

        public string SearchFilter
        {
            get => _searchFilter;
            set
            {
                if (SetProperty(ref _searchFilter, value))
                    UpdateVisible();
            }
        }

        public LauncherPage SelectedPage
        {
            get => _selectedPage;
            set
            {
                if (SetProperty(ref _selectedPage, value) && value != null)
                    SelectedRepo = null;
            }
        }

        public RepositoryNode SelectedRepo
        {
            get => _selectedRepo;
            set
            {
                if (SetProperty(ref _selectedRepo, value) && value != null)
                    SelectedPage = null;
            }
        }

        public QuickLauncher(Launcher launcher)
        {
            _launcher = launcher;

            foreach (var page in _launcher.Pages)
            {
                if (page.Node.IsRepository)
                    _opened.Add(page.Node.Id);
            }

            UpdateVisible();
        }

        public void ClearFilter()
        {
            SearchFilter = string.Empty;
        }

        public void OpenOrSwitchTo()
        {
            if (_selectedPage != null)
                _launcher.ActivePage = _selectedPage;
            else if (_selectedRepo != null)
                _launcher.OpenRepositoryInTab(_selectedRepo, null);

            _launcher.QuickLauncher = null;
        }

        private void UpdateVisible()
        {
            var pages = new List<LauncherPage>();
            foreach (var page in _launcher.Pages)
            {
                if (string.IsNullOrEmpty(_searchFilter) ||
                    page.Node.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                    (page.Node.IsRepository && page.Node.Id.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase)))
                    pages.Add(page);
            }

            var repos = new List<RepositoryNode>();
            CollectVisibleRepository(repos, Preferences.Instance.RepositoryNodes);

            VisiblePages = pages;
            VisibleRepos = repos;
        }

        private void CollectVisibleRepository(List<RepositoryNode> outs, List<RepositoryNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (!node.IsRepository)
                {
                    CollectVisibleRepository(outs, node.SubNodes);
                    continue;
                }

                if (_opened.Contains(node.Id))
                    continue;

                if (string.IsNullOrEmpty(_searchFilter) ||
                    node.Id.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                    node.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                    outs.Add(node);
            }
        }

        private Launcher _launcher = null;
        private HashSet<string> _opened = new HashSet<string>();
        private List<LauncherPage> _visiblePages = [];
        private List<RepositoryNode> _visibleRepos = [];
        private string _searchFilter = string.Empty;
        private LauncherPage _selectedPage = null;
        private RepositoryNode _selectedRepo = null;
    }
}
