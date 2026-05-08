using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace SourceGit.Views
{
    public partial class CommitBaseInfo : UserControl
    {
        public static readonly StyledProperty<Models.CommitFullMessage> FullMessageProperty =
            AvaloniaProperty.Register<CommitBaseInfo, Models.CommitFullMessage>(nameof(FullMessage));

        public Models.CommitFullMessage FullMessage
        {
            get => GetValue(FullMessageProperty);
            set => SetValue(FullMessageProperty, value);
        }

        public static readonly StyledProperty<Models.CommitSignInfo> SignInfoProperty =
            AvaloniaProperty.Register<CommitBaseInfo, Models.CommitSignInfo>(nameof(SignInfo));

        public Models.CommitSignInfo SignInfo
        {
            get => GetValue(SignInfoProperty);
            set => SetValue(SignInfoProperty, value);
        }

        public static readonly StyledProperty<bool> SupportsContainsInProperty =
            AvaloniaProperty.Register<CommitBaseInfo, bool>(nameof(SupportsContainsIn));

        public bool SupportsContainsIn
        {
            get => GetValue(SupportsContainsInProperty);
            set => SetValue(SupportsContainsInProperty, value);
        }

        public static readonly StyledProperty<List<Models.CommitLink>> WebLinksProperty =
            AvaloniaProperty.Register<CommitBaseInfo, List<Models.CommitLink>>(nameof(WebLinks));

        public List<Models.CommitLink> WebLinks
        {
            get => GetValue(WebLinksProperty);
            set => SetValue(WebLinksProperty, value);
        }

        public static readonly StyledProperty<List<string>> ChildrenProperty =
            AvaloniaProperty.Register<CommitBaseInfo, List<string>>(nameof(Children));

        public List<string> Children
        {
            get => GetValue(ChildrenProperty);
            set => SetValue(ChildrenProperty, value);
        }

        public static readonly StyledProperty<bool> IsSHACopiedProperty =
            AvaloniaProperty.Register<CommitBaseInfo, bool>(nameof(IsSHACopied));

        public bool IsSHACopied
        {
            get => GetValue(IsSHACopiedProperty);
            set => SetValue(IsSHACopiedProperty, value);
        }

        public CommitBaseInfo()
        {
            InitializeComponent();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ContentProperty)
            {
                _iconResetTimer?.Dispose();
                SetCurrentValue(IsSHACopiedProperty, false);
            }
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);
            _iconResetTimer?.Dispose();
        }

        private async void OnCopyCommitSHA(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: Models.Commit commit })
                await this.CopyTextAsync(commit.SHA);

            _iconResetTimer = DispatcherTimer.RunOnce(() =>
            {
                if (IsSHACopied)
                    IsSHACopied = false;

                _iconResetTimer = null;
            }, TimeSpan.FromSeconds(2));

            IsSHACopied = true;
            e.Handled = true;
        }

        private void OnOpenWebLink(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.CommitDetail detail && sender is Control control)
            {
                var links = WebLinks;
                if (links.Count > 1)
                {
                    var menu = new ContextMenu();

                    foreach (var link in links)
                    {
                        var url = $"{link.URLPrefix}{detail.Commit.SHA}";
                        var item = new MenuItem() { Header = link.Name };
                        item.Click += (_, ev) =>
                        {
                            Native.OS.OpenBrowser(url);
                            ev.Handled = true;
                        };

                        menu.Items.Add(item);
                    }

                    menu.Open(control);
                }
                else if (links.Count == 1)
                {
                    var url = $"{links[0].URLPrefix}{detail.Commit.SHA}";
                    Native.OS.OpenBrowser(url);
                }
            }

            e.Handled = true;
        }

        private async void OnOpenContainsIn(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.CommitDetail detail && sender is Button button)
            {
                var tracking = new CommitRelationTracking();
                var flyout = new Flyout();
                flyout.Content = tracking;
                flyout.ShowAt(button);

                await tracking.SetDataAsync(detail);
            }

            e.Handled = true;
        }

        private async void OnSHAPointerEntered(object sender, PointerEventArgs e)
        {
            if (DataContext is ViewModels.CommitDetail detail && sender is Control { DataContext: string sha } ctl)
            {
                var tooltip = ToolTip.GetTip(ctl);
                if (tooltip is Models.Commit commit && commit.SHA.Equals(sha, StringComparison.Ordinal))
                    return;

                var c = await detail.GetCommitAsync(sha);
                if (c is not null && ctl is { IsEffectivelyVisible: true, DataContext: string newSHA } && sha.Equals(newSHA, StringComparison.Ordinal))
                    ToolTip.SetTip(ctl, c);
            }

            e.Handled = true;
        }

        private void OnSHAPressed(object sender, PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            if (point.Properties.IsLeftButtonPressed &&
                DataContext is ViewModels.CommitDetail detail &&
                sender is Control { DataContext: string sha })
                detail.NavigateTo(sha);

            e.Handled = true;
        }

        private void OnUserContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is not Control { Tag: Models.User user } control)
                return;

            var copyName = new MenuItem();
            copyName.Header = App.Text("CommitDetail.Info.CopyName");
            copyName.Icon = this.CreateMenuIcon("Icons.Copy");
            copyName.Click += async (_, ev) =>
            {
                await this.CopyTextAsync(user.Name);
                ev.Handled = true;
            };

            var copyEmail = new MenuItem();
            copyEmail.Header = App.Text("CommitDetail.Info.CopyEmail");
            copyEmail.Icon = this.CreateMenuIcon("Icons.Email");
            copyEmail.Click += async (_, ev) =>
            {
                await this.CopyTextAsync(user.Email);
                ev.Handled = true;
            };

            var copyUser = new MenuItem();
            copyUser.Header = App.Text("CommitDetail.Info.CopyNameAndEmail");
            copyUser.Icon = this.CreateMenuIcon("Icons.User");
            copyUser.Click += async (_, ev) =>
            {
                await this.CopyTextAsync(user.ToString());
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(copyName);
            menu.Items.Add(copyEmail);
            menu.Items.Add(copyUser);
            menu.Open(control);
            e.Handled = true;
        }

        private async void OnCopyAllCommitMessage(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.CommitDetail detail)
                await this.CopyTextAsync(detail.FullMessage.Message);
            e.Handled = true;
        }

        private void OnCommitRefsPresenterPointerReleased(object sender, PointerReleasedEventArgs e)
        {
            e.Handled = true;

            if (DataContext is ViewModels.CommitDetail detail &&
                sender is CommitRefsPresenter presenter &&
                e.Properties.PointerUpdateKind == PointerUpdateKind.RightButtonReleased)
            {
                var decorator = presenter.DecoratorAt(e.GetPosition(presenter));
                if (decorator != null)
                {
                    var copy = new MenuItem();
                    copy.Icon = this.CreateMenuIcon("Icons.Copy");
                    copy.Header = App.Text("Copy");
                    copy.Click += async (_, ev) =>
                    {
                        await this.CopyTextAsync(decorator.Name);
                        ev.Handled = true;
                    };

                    var menu = new ContextMenu();
                    menu.Items.Add(copy);
                    menu.Open(presenter);
                }
            }
        }

        private IDisposable _iconResetTimer;
    }
}
