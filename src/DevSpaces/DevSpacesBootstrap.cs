using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace DevBoard.DevSpaces
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

        private static void OnRepositoryLoaded(Views.Repository view, RoutedEventArgs e)
        {
            if (view.DataContext is not ViewModels.Repository repository)
                return;

            if (_repositoryViews.TryGetValue(view, out _))
                return;

            var integration = RepositoryIntegration.TryCreate(view, repository);
            if (integration != null)
                _repositoryViews.Add(view, integration);
        }

        private static void OnRepositoryUnloaded(Views.Repository view, RoutedEventArgs e)
        {
            if (!_repositoryViews.TryGetValue(view, out var integration))
                return;

            integration.Detach();
            _repositoryViews.Remove(view);
        }

        private static void OnPreferencesLoaded(Views.Preferences view, RoutedEventArgs e)
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

                var item = CreateNavigationItem(view, out var label, out var badge, out var badgeLabel);
                pageSwitcher.Items.Add(item);

                var host = new Border
                {
                    IsVisible = false,
                    Opacity = 0,
                    IsHitTestVisible = false,
                };
                rightPages.Children.Add(host);

                return new RepositoryIntegration(repository, item, label, badge, badgeLabel, host);
            }

            private RepositoryIntegration(
                ViewModels.Repository repository,
                ListBoxItem navigationItem,
                TextBlock navigationLabel,
                Border navigationBadge,
                TextBlock navigationBadgeLabel,
                Border host)
            {
                _repository = repository;
                _navigationItem = navigationItem;
                _navigationLabel = navigationLabel;
                _navigationBadge = navigationBadge;
                _navigationBadgeLabel = navigationBadgeLabel;
                _host = host;

                _repository.PropertyChanged += OnRepositoryPropertyChanged;
                ViewModels.Preferences.Instance.PropertyChanged += OnPreferencesPropertyChanged;

                WireToolNavigation();

                if (ViewModels.Preferences.Instance.EnableDevSpaces)
                    AttachSpaces();

                Update();
            }

            public void Detach()
            {
                if (_host.Child is Views.DevSpaces spacesView)
                    spacesView.SetPageActive(false);

                _repository.PropertyChanged -= OnRepositoryPropertyChanged;
                ViewModels.Preferences.Instance.PropertyChanged -= OnPreferencesPropertyChanged;
                DetachSpaces();
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

            private void OnSessionsChanged(object sender, NotifyCollectionChangedEventArgs e)
            {
                UpdateNavigationLabel();
            }

            private void AttachSpaces()
            {
                if (_spaces != null)
                    return;

                _spaces = DevSpaceRegistry.Attach(_repository, _host);
                if (_spaces != null)
                    _spaces.Sessions.CollectionChanged += OnSessionsChanged;

                UpdateNavigationLabel();
            }

            private void DetachSpaces()
            {
                if (_spaces != null)
                    _spaces.Sessions.CollectionChanged -= OnSessionsChanged;

                _spaces = null;
                UpdateNavigationLabel();
            }

            private void ActivateFiles()
            {
                ActivateTool(spaces => spaces.ActivateFiles());
            }

            private void ActivateAIRouter()
            {
                ActivateTool(spaces => spaces.ActivateAIRouter());
            }

            private void ActivateTool(Action<ViewModels.DevSpaces> activate)
            {
                if (!ViewModels.Preferences.Instance.EnableDevSpaces)
                    return;

                AttachSpaces();
                _repository.SelectedViewIndex = 3;

                if (_spaces != null)
                    activate(_spaces);

                Update();
            }

            private void WireToolNavigation()
            {
                if (_navigationItem.Content is not StackPanel root || root.Children.Count < 2 || root.Children[1] is not StackPanel tools)
                    return;

                foreach (var button in tools.Children.OfType<Button>())
                {
                    if (button.Tag is not string target)
                        continue;

                    if (target == "Files")
                        button.Click += (_, e) =>
                        {
                            ActivateFiles();
                            e.Handled = true;
                        };
                    else if (target == "AIRouter")
                        button.Click += (_, e) =>
                        {
                            ActivateAIRouter();
                            e.Handled = true;
                        };
                }
            }

            private void Update()
            {
                var enabled = ViewModels.Preferences.Instance.EnableDevSpaces;
                _navigationItem.IsVisible = enabled;

                if (!enabled)
                {
                    if (_host.Child is Views.DevSpaces spacesView)
                        spacesView.SetPageActive(false);

                    _host.IsVisible = false;
                    _host.Opacity = 0;
                    _host.IsHitTestVisible = false;
                    DetachSpaces();

                    if (_repository.SelectedViewIndex == 3)
                        _repository.SelectedViewIndex = 0;
                    return;
                }

                AttachSpaces();

                // Keep the terminal subtree mounted and measured while another repository page
                // is active. Hiding with IsVisible would collapse the Avalonia fallback and
                // force its TUI to resize/reload when returning to DevSpaces. Native HWNDs are
                // hidden separately by DevSpaces.SetPageActive.
                _host.IsVisible = true;
                var active = _repository.SelectedViewIndex == 3;
                _host.Opacity = active ? 1 : 0;
                _host.IsHitTestVisible = active;

                if (_host.Child is Views.DevSpaces activeSpacesView)
                    activeSpacesView.SetPageActive(active);

                if (active)
                    _spaces?.EnsureFirstSession();
            }

            private void UpdateNavigationLabel()
            {
                var count = _spaces?.Sessions.Count ?? 0;
                _navigationLabel.Text = App.Text("DevSpaces");
                _navigationBadge.IsVisible = count > 0;
                _navigationBadgeLabel.Text = count.ToString();
            }

            private static ListBoxItem CreateNavigationItem(
                Views.Repository view,
                out TextBlock label,
                out Border badge,
                out TextBlock badgeLabel)
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

                label = new TextBlock
                {
                    Text = App.Text("DevSpaces"),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                label.Classes.Add("header");

                badgeLabel = new TextBlock
                {
                    Text = "0",
                    FontSize = 10,
                };
                badgeLabel.Bind(TextBlock.ForegroundProperty, view.GetResourceObservable("Brush.BadgeFG"));
                badgeLabel.Bind(TextBlock.FontFamilyProperty, view.GetResourceObservable("Fonts.Monospace"));

                badge = new Border
                {
                    Height = 18,
                    Margin = new Thickness(6, 0),
                    Padding = new Thickness(9, 0),
                    CornerRadius = new CornerRadius(9),
                    VerticalAlignment = VerticalAlignment.Center,
                    IsVisible = false,
                    Child = badgeLabel,
                };
                badge.Bind(Border.BackgroundProperty, view.GetResourceObservable("Brush.Badge"));

                var tools = new StackPanel
                {
                    Margin = new Thickness(20, 0, 0, 2),
                    IsVisible = true,
                };
                tools.Children.Add(CreateToolButton(view, "Icons.Folder", App.Text("DevSpaces.Files"), "Files"));
                tools.Children.Add(CreateToolButton(view, "Icons.AIAssist", "AI Router", "AIRouter"));

                var expanderLabel = new TextBlock
                {
                    Text = "▾",
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                };
                var expander = new ToggleButton
                {
                    Width = 24,
                    Height = 24,
                    Margin = new Thickness(2, 0, 2, 0),
                    IsChecked = true,
                    Content = expanderLabel,
                    ToolTip = App.Text("DevSpaces"),
                };
                expander.Classes.Add("icon_button");
                expander.Click += (_, _) =>
                {
                    tools.IsVisible = expander.IsChecked != false;
                    expanderLabel.Text = tools.IsVisible ? "▾" : "▸";
                };

                var header = new Grid
                {
                    Height = 28,
                    ColumnDefinitions = new ColumnDefinitions("4,Auto,*,Auto,Auto"),
                };
                header.Children.Add(indicator);
                Grid.SetColumn(icon, 1);
                header.Children.Add(icon);
                Grid.SetColumn(label, 2);
                header.Children.Add(label);
                Grid.SetColumn(badge, 3);
                header.Children.Add(badge);
                Grid.SetColumn(expander, 4);
                header.Children.Add(expander);

                var content = new StackPanel();
                content.Children.Add(header);
                content.Children.Add(tools);

                return new ListBoxItem
                {
                    Height = double.NaN,
                    Content = content,
                };
            }

            private static Button CreateToolButton(Views.Repository view, string iconKey, string text, string tag)
            {
                var icon = new Path
                {
                    Width = 12,
                    Height = 12,
                    Margin = new Thickness(6, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                if (view.TryFindResource(iconKey, out var iconResource) && iconResource is Geometry geometry)
                    icon.Data = geometry;

                var label = new TextBlock
                {
                    Text = text,
                    VerticalAlignment = VerticalAlignment.Center,
                };

                var content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                };
                content.Children.Add(icon);
                content.Children.Add(label);

                var button = new Button
                {
                    Height = 26,
                    Padding = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Tag = tag,
                    Content = content,
                };
                button.Classes.Add("flat");
                return button;
            }

            private readonly ViewModels.Repository _repository;
            private readonly ListBoxItem _navigationItem;
            private readonly TextBlock _navigationLabel;
            private readonly Border _navigationBadge;
            private readonly TextBlock _navigationBadgeLabel;
            private readonly Border _host;
            private ViewModels.DevSpaces _spaces;
        }

        private static readonly ConditionalWeakTable<Views.Repository, RepositoryIntegration> _repositoryViews = new();
        private static readonly ConditionalWeakTable<Views.Preferences, object> _preferencesViews = new();
    }
}
