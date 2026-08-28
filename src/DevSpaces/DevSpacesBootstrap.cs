using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace SourceGit.DevSpaces
{
    internal static class DevSpacesBootstrap
    {
        [ModuleInitializer]
        public static void Initialize()
        {
            Control.LoadedEvent.AddClassHandler<Views.Repository>(OnRepositoryLoaded);
            Control.UnloadedEvent.AddClassHandler<Views.Repository>(OnRepositoryUnloaded);
            Control.LoadedEvent.AddClassHandler<Views.Preferences>(OnPreferencesLoaded);
        }

        private static void OnRepositoryLoaded(Views.Repository view, RoutedEventArgs _)
        {
            if (view.DataContext is not ViewModels.Repository repository)
                return;

            if (_repositoryViews.TryGetValue(view, out _))
                return;

            var integration = RepositoryIntegration.TryCreate(view, repository);
            if (integration != null)
                _repositoryViews.Add(view, integration);
        }

        private static void OnRepositoryUnloaded(Views.Repository view, RoutedEventArgs _)
        {
            if (!_repositoryViews.TryGetValue(view, out var integration))
                return;

            integration.Detach();
            _repositoryViews.Remove(view);
        }

        private static void OnPreferencesLoaded(Views.Preferences view, RoutedEventArgs _)
        {
            if (_preferencesViews.TryGetValue(view, out _))
                return;

            var tabs = view.FindDescendantOfType<TabControl>();
            if (tabs == null || tabs.ItemsSource != null)
                return;

            var tab = new TabItem
            {
                Header = App.Text("DevSpaces"),
                Content = new Views.DevSpacesPreferences
                {
                    DataContext = ViewModels.Preferences.Instance,
                },
            };

            tabs.Items.Add(tab);
            _preferencesViews.Add(view, new object());
        }

        private sealed class RepositoryIntegration
        {
            public static RepositoryIntegration TryCreate(Views.Repository view, ViewModels.Repository repository)
            {
                if (view.Content is not Grid root)
                    return null;

                var leftPanel = root.Children
                    .OfType<Grid>()
                    .FirstOrDefault(x => Grid.GetColumn(x) == 0);
                var dashboard = leftPanel?.Children
                    .OfType<Grid>()
                    .FirstOrDefault(x => Grid.GetRow(x) == 1 && x.RowDefinitions.Count == 3);
                var pageSwitcherBorder = dashboard?.Children
                    .OfType<Border>()
                    .FirstOrDefault(x => Grid.GetRow(x) == 0);
                var pageSwitcher = pageSwitcherBorder?.Child as ListBox;

                var rightPanel = root.Children
                    .OfType<Grid>()
                    .FirstOrDefault(x => Grid.GetColumn(x) == 2);
                var rightPages = rightPanel?.Children
                    .OfType<Grid>()
                    .FirstOrDefault(x => Grid.GetRow(x) == 3);

                if (pageSwitcher == null || rightPages == null || pageSwitcher.ItemsSource != null)
                    return null;

                var item = CreateNavigationItem(view);
                pageSwitcher.Items.Add(item);

                var host = new Border
                {
                    IsVisible = false,
                };
                rightPages.Children.Add(host);

                return new RepositoryIntegration(repository, item, host);
            }

            private RepositoryIntegration(
                ViewModels.Repository repository,
                ListBoxItem navigationItem,
                Border host)
            {
                _repository = repository;
                _navigationItem = navigationItem;
                _host = host;

                _repository.PropertyChanged += OnRepositoryPropertyChanged;
                ViewModels.Preferences.Instance.PropertyChanged += OnPreferencesPropertyChanged;
                Update();
            }

            public void Detach()
            {
                _repository.PropertyChanged -= OnRepositoryPropertyChanged;
                ViewModels.Preferences.Instance.PropertyChanged -= OnPreferencesPropertyChanged;
            }

            private void OnRepositoryPropertyChanged(object sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(ViewModels.Repository.SelectedViewIndex))
                    Update();
            }

            private void OnPreferencesPropertyChanged(object sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(ViewModels.Preferences.EnableDevSpaces))
                    Update();
            }

            private void Update()
            {
                var enabled = ViewModels.Preferences.Instance.EnableDevSpaces;
                _navigationItem.IsVisible = enabled;

                if (!enabled)
                {
                    _host.IsVisible = false;
                    if (_repository.SelectedViewIndex == 3)
                        _repository.SelectedViewIndex = 0;
                    return;
                }

                var active = _repository.SelectedViewIndex == 3;
                _host.IsVisible = active;
                if (!active)
                    return;

                var spaces = DevSpaceRegistry.Attach(_repository, _host);
                spaces?.EnsureFirstSession();
            }

            private static ListBoxItem CreateNavigationItem(Views.Repository view)
            {
                var indicator = new Rectangle
                {
                    Width = 4,
                    Height = 20,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                indicator.Classes.Add("indicator");

                var icon = new Path
                {
                    Width = 12,
                    Height = 12,
                    Margin = new Thickness(6, 0),
                };
                icon.Classes.Add("icon");
                if (view.TryFindResource("Icons.Terminal", out var iconResource) && iconResource is Geometry geometry)
                    icon.Data = geometry;

                var label = new TextBlock
                {
                    Text = App.Text("DevSpaces"),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                label.Classes.Add("header");

                var content = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("4,Auto,*"),
                };
                content.Children.Add(indicator);
                Grid.SetColumn(icon, 1);
                content.Children.Add(icon);
                Grid.SetColumn(label, 2);
                content.Children.Add(label);

                return new ListBoxItem
                {
                    Content = content,
                };
            }

            private readonly ViewModels.Repository _repository;
            private readonly ListBoxItem _navigationItem;
            private readonly Border _host;
        }

        private static readonly ConditionalWeakTable<Views.Repository, RepositoryIntegration> _repositoryViews = new();
        private static readonly ConditionalWeakTable<Views.Preferences, object> _preferencesViews = new();
    }
}
