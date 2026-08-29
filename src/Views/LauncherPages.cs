using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

using Avalonia.Collections;
using Avalonia.Controls;

namespace DevBoard.Views
{
    public sealed class LauncherPages : Grid
    {
        public LauncherPages()
        {
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, EventArgs e)
        {
            Attach(DataContext as ViewModels.Launcher);
        }

        private void Attach(ViewModels.Launcher launcher)
        {
            if (ReferenceEquals(_launcher, launcher))
                return;

            if (_launcher != null)
                _launcher.PropertyChanged -= OnLauncherPropertyChanged;

            AttachPages(null);
            _launcher = launcher;

            if (_launcher != null)
            {
                _launcher.PropertyChanged += OnLauncherPropertyChanged;
                AttachPages(_launcher.Pages);
            }
            else
            {
                ClearCachedPages();
            }
        }

        private void AttachPages(AvaloniaList<ViewModels.LauncherPage> pages)
        {
            if (_pages != null)
                _pages.CollectionChanged -= OnPagesCollectionChanged;

            _pages = pages;

            if (_pages != null)
                _pages.CollectionChanged += OnPagesCollectionChanged;

            SyncPages();
        }

        private void OnLauncherPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModels.Launcher.Pages))
                AttachPages(_launcher?.Pages);
            else if (e.PropertyName == nameof(ViewModels.Launcher.ActivePage))
                UpdateActivePage();
        }

        private void OnPagesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            SyncPages();
        }

        private void SyncPages()
        {
            if (_launcher == null || _pages == null)
            {
                ClearCachedPages();
                return;
            }

            var current = new HashSet<ViewModels.LauncherPage>(_pages);
            var removed = new List<ViewModels.LauncherPage>();
            foreach (var pair in _views)
            {
                if (!current.Contains(pair.Key))
                    removed.Add(pair.Key);
            }

            foreach (var page in removed)
            {
                if (_views.Remove(page, out var view))
                    Children.Remove(view);
            }

            foreach (var page in _pages)
            {
                if (_views.ContainsKey(page))
                    continue;

                var view = new LauncherPage
                {
                    DataContext = page,
                    Opacity = 0,
                    IsHitTestVisible = false,
                    IsEnabled = false,
                };
                _views.Add(page, view);
                Children.Add(view);
            }

            UpdateActivePage();
        }

        private void UpdateActivePage()
        {
            var activePage = _launcher?.ActivePage;
            foreach (var pair in _views)
            {
                var active = ReferenceEquals(pair.Key, activePage);
                pair.Value.Opacity = active ? 1 : 0;
                pair.Value.IsHitTestVisible = active;
                pair.Value.IsEnabled = active;
                pair.Value.ZIndex = active ? 1 : 0;
            }
        }

        private void ClearCachedPages()
        {
            _views.Clear();
            Children.Clear();
        }

        private readonly Dictionary<ViewModels.LauncherPage, LauncherPage> _views = [];
        private ViewModels.Launcher _launcher;
        private AvaloniaList<ViewModels.LauncherPage> _pages;
    }
}
