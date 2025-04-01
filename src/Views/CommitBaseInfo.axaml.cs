using System.Collections.Generic;
using System.Threading.Tasks;

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

        public CommitBaseInfo()
        {
            InitializeComponent();
        }

        private void OnCopyCommitSHA(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: Models.Commit commit })
                App.CopyText(commit.SHA);

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

        private void OnOpenContainsIn(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.CommitDetail detail && sender is Button button)
            {
                var tracking = new CommitRelationTracking(detail);
                var flyout = new Flyout();
                flyout.Content = tracking;
                flyout.ShowAt(button);
            }

            e.Handled = true;
        }

        private void OnSHAPointerEntered(object sender, PointerEventArgs e)
        {
            if (DataContext is ViewModels.CommitDetail detail && sender is Control { DataContext: string sha } ctl)
            {
                var tooltip = ToolTip.GetTip(ctl);
                if (tooltip is Models.Commit commit && commit.SHA == sha)
                    return;

                Task.Run(() =>
                {
                    var c = detail.GetParent(sha);
                    if (c == null)
                        return;

                    Dispatcher.UIThread.Invoke(() =>
                    {
                        if (ctl.IsEffectivelyVisible && ctl.DataContext is string newSHA && newSHA == sha)
                        {
                            ToolTip.SetTip(ctl, c);

                            if (ctl.IsPointerOver)
                                ToolTip.SetIsOpen(ctl, true);
                        }
                    });
                });
            }

            e.Handled = true;
        }

        private void OnSHAPressed(object sender, PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);

            if (point.Properties.IsLeftButtonPressed && DataContext is ViewModels.CommitDetail detail && sender is Control { DataContext: string sha })
            {
                detail.NavigateTo(sha);
            }

            e.Handled = true;
        }
    }
}
