using System;
using System.Collections.Generic;

namespace SourceGit.ViewModels
{
    public class LauncherPagesCommandPalette : ICommandPalette
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

        public LauncherPagesCommandPalette(Launcher launcher)
        {
            _launcher = launcher;

            foreach (var page in _launcher.Pages)
            {
                if (page.Node.IsRepository)
                    _opened.Add(page.Node.Id);
            }

            UpdateVisible();
        }

        public override void Cleanup()
        {
            _launcher = null;
            _opened.Clear();
            _visiblePages.Clear();
            _visibleRepos.Clear();
            _searchFilter = null;
            _selectedPage = null;
            _selectedRepo = null;
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

            _launcher?.CancelCommandPalette();
        }

        private void UpdateVisible()
        {
            var pages = new List<LauncherPage>();
            CollectVisiblePages(pages);

            var repos = new List<RepositoryNode>();
            CollectVisibleRepository(repos, Preferences.Instance.RepositoryNodes);

            var autoSelectPage = _selectedPage;
            var autoSelectRepo = _selectedRepo;

            if (_selectedPage != null)
            {
                if (pages.Contains(_selectedPage))
                {
                    // Keep selection
                }
                else if (pages.Count > 0)
                {
                    autoSelectPage = pages[0];
                }
                else if (repos.Count > 0)
                {
                    autoSelectPage = null;
                    autoSelectRepo = repos[0];
                }
                else
                {
                    autoSelectPage = null;
                }
            }
            else if (_selectedRepo != null)
            {
                if (repos.Contains(_selectedRepo))
                {
                    // Keep selection
                }
                else if (repos.Count > 0)
                {
                    autoSelectRepo = repos[0];
                }
                else if (pages.Count > 0)
                {
                    autoSelectPage = pages[0];
                    autoSelectRepo = null;
                }
                else
                {
                    autoSelectRepo = null;
                }
            }
            else if (pages.Count > 0)
            {
                autoSelectPage = pages[0];
                autoSelectRepo = null;
            }
            else if (repos.Count > 0)
            {
                autoSelectPage = null;
                autoSelectRepo = repos[0];
            }
            else
            {
                autoSelectPage = null;
                autoSelectRepo = null;
            }

            VisiblePages = pages;
            VisibleRepos = repos;
            SelectedPage = autoSelectPage;
            SelectedRepo = autoSelectRepo;
        }

        private void CollectVisiblePages(List<LauncherPage> pages)
        {
            foreach (var page in _launcher.Pages)
            {
                if (page == _launcher.ActivePage)
                    continue;

                if (string.IsNullOrEmpty(_searchFilter) ||
                    page.Node.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                    (page.Node.IsRepository && page.Node.Id.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase)))
                    pages.Add(page);
            }
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
