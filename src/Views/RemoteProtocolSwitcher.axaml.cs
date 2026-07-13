using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class RemoteProtocolSwitcher : UserControl
    {
        public static readonly DirectProperty<RemoteProtocolSwitcher, string> UrlProperty =
            AvaloniaProperty.RegisterDirect<RemoteProtocolSwitcher, string>(
                nameof(Url),
                static o => o.Url,
                static (o, v) => o.Url = v);

        public string Url
        {
            get => _url;
            set => SetAndRaise(UrlProperty, ref _url, value);
        }

        public static readonly DirectProperty<RemoteProtocolSwitcher, string> ActiveProtocolProperty =
            AvaloniaProperty.RegisterDirect<RemoteProtocolSwitcher, string>(
                nameof(ActiveProtocol),
                static o => o.ActiveProtocol);

        public string ActiveProtocol
        {
            get => _activeProtocol;
            set => SetAndRaise(ActiveProtocolProperty, ref _activeProtocol, value);
        }

        public RemoteProtocolSwitcher()
        {
            InitializeComponent();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == UrlProperty)
            {
                _protocols.Clear();

                var url = _url ?? string.Empty;
                if (url.StartsWith("https://", StringComparison.Ordinal) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    var host = uri.Host;
                    var route = uri.AbsolutePath.TrimStart('/');

                    _protocols.Add(url);
                    _protocols.Add($"git@{host}:{route}");

                    ActiveProtocol = "HTTPS";
                    SetCurrentValue(IsVisibleProperty, true);
                    return;
                }

                var match = REG_SSH_FORMAT().Match(url);
                if (match.Success)
                {
                    var host = match.Groups[1].Value;
                    var repo = match.Groups[2].Value;

                    _protocols.Add($"https://{host}/{repo}");
                    _protocols.Add(url);

                    ActiveProtocol = "SSH";
                    SetCurrentValue(IsVisibleProperty, true);
                    return;
                }

                SetCurrentValue(IsVisibleProperty, false);
            }
        }

        private void OnOpenDropdownMenu(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && _protocols.Count > 0)
            {
                var menu = new ContextMenu();
                menu.Placement = PlacementMode.BottomEdgeAlignedLeft;

                foreach (var protocol in _protocols)
                {
                    var dup = protocol;
                    var item = new MenuItem() { Header = dup };
                    item.Click += (_, _) => Url = protocol;
                    menu.Items.Add(item);
                }

                menu.Open(btn);
            }

            e.Handled = true;
        }

        [GeneratedRegex(@"^git@([\w\.\-]+):(.+)$")]
        private static partial Regex REG_SSH_FORMAT();

        private string _url = string.Empty;
        private string _activeProtocol = string.Empty;
        private List<string> _protocols = [];
    }
}
