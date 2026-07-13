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
        public static readonly DirectProperty<CommitBaseInfo, Models.CommitFullMessage> FullMessageProperty =
            AvaloniaProperty.RegisterDirect<CommitBaseInfo, Models.CommitFullMessage>(
                nameof(FullMessage),
                static o => o.FullMessage,
                static (o, v) => o.FullMessage = v);

        public Models.CommitFullMessage FullMessage
        {
            get => _fullMessage;
            set => SetAndRaise(FullMessageProperty, ref _fullMessage, value);
        }

        public static readonly DirectProperty<CommitBaseInfo, Models.CommitSignInfo> SignInfoProperty =
            AvaloniaProperty.RegisterDirect<CommitBaseInfo, Models.CommitSignInfo>(
                nameof(SignInfo),
                static o => o.SignInfo,
                static (o, v) => o.SignInfo = v);

        public Models.CommitSignInfo SignInfo
        {
            get => _signInfo;
            set => SetAndRaise(SignInfoProperty, ref _signInfo, value);
        }

        public static readonly DirectProperty<CommitBaseInfo, bool> SupportsContainsInProperty =
            AvaloniaProperty.RegisterDirect<CommitBaseInfo, bool>(
                nameof(SupportsContainsIn),
                static o => o.SupportsContainsIn,
                static (o, v) => o.SupportsContainsIn = v);

        public bool SupportsContainsIn
        {
            get => _supportsContainsIn;
            set => SetAndRaise(SupportsContainsInProperty, ref _supportsContainsIn, value);
        }

        public static readonly DirectProperty<CommitBaseInfo, List<Models.CommitLink>> WebLinksProperty =
            AvaloniaProperty.RegisterDirect<CommitBaseInfo, List<Models.CommitLink>>(
                nameof(WebLinks),
                static o => o.WebLinks,
                static (o, v) => o.WebLinks = v);

        public List<Models.CommitLink> WebLinks
        {
            get => _webLinks;
            set => SetAndRaise(WebLinksProperty, ref _webLinks, value);
        }

        public static readonly DirectProperty<CommitBaseInfo, List<string>> ChildrenProperty =
            AvaloniaProperty.RegisterDirect<CommitBaseInfo, List<string>>(
                nameof(Children),
                static o => o.Children,
                static (o, v) => o.Children = v);

        public List<string> Children
        {
            get => _children;
            set => SetAndRaise(ChildrenProperty, ref _children, value);
        }

        public static readonly DirectProperty<CommitBaseInfo, bool> IsSHACopiedProperty =
            AvaloniaProperty.RegisterDirect<CommitBaseInfo, bool>(
                nameof(IsSHACopied),
                static o => o.IsSHACopied);

        public bool IsSHACopied
        {
            get => _isSHACopied;
            set => SetAndRaise(IsSHACopiedProperty, ref _isSHACopied, value);
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
                IsSHACopied = false;
                _iconResetTimer?.Stop();
            }
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            _iconResetTimer = new DispatcherTimer();
            _iconResetTimer.Interval = TimeSpan.FromSeconds(1.5);
            _iconResetTimer.Tag = this;
            _iconResetTimer.Tick += static (o, _) =>
            {
                if (o is DispatcherTimer { Tag: CommitBaseInfo view } timer)
                {
                    if (view.IsSHACopied)
                        view.IsSHACopied = false;

                    timer.IsEnabled = false;
                }
            };
            _iconResetTimer.IsEnabled = false;
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            _iconResetTimer.Tag = null;
            _iconResetTimer.IsEnabled = false;

            base.OnUnloaded(e);
        }

        private void OnDateTimeContextMenuRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is DateTimePresenter presenter)
            {
                var copy = new MenuItem();
                copy.Header = App.Text("Copy");
                copy.Icon = this.CreateMenuIcon("Icons.Copy");
                copy.Click += async (_, ev) =>
                {
                    await this.CopyTextAsync(presenter.Text);
                    ev.Handled = true;
                };

                var menu = new ContextMenu();
                menu.Items.Add(copy);
                menu.Open(presenter);
                e.Handled = true;
            }
        }

        private async void OnCopyCommitSHA(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: Models.Commit commit })
                await this.CopyTextAsync(commit.SHA);

            IsSHACopied = true;
            _iconResetTimer?.Start();
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

            if (DataContext is ViewModels.CommitDetail &&
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

        private Models.CommitFullMessage _fullMessage = null;
        private Models.CommitSignInfo _signInfo = null;
        private bool _supportsContainsIn = false;
        private List<Models.CommitLink> _webLinks = null;
        private List<string> _children = null;
        private bool _isSHACopied = false;
        private DispatcherTimer _iconResetTimer = null;
    }
}
