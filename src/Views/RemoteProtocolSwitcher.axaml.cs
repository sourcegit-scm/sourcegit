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
        public static readonly StyledProperty<string> UrlProperty =
            AvaloniaProperty.Register<RemoteProtocolSwitcher, string>(nameof(Url));

        public string Url
        {
            get => GetValue(UrlProperty);
            set => SetValue(UrlProperty, value);
        }

        public static readonly StyledProperty<string> ActiveProtocolProperty =
            AvaloniaProperty.Register<RemoteProtocolSwitcher, string>(nameof(ActiveProtocol));

        public string ActiveProtocol
        {
            get => GetValue(ActiveProtocolProperty);
            set => SetValue(ActiveProtocolProperty, value);
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

                var url = Url ?? string.Empty;
                if (url.StartsWith("https://", StringComparison.Ordinal) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    var host = uri.Host;
                    var serverName = uri.Port == 443 ? host : $"{host}:{uri.Port}";
                    var route = uri.AbsolutePath.TrimStart('/');

                    _protocols.Add(url);
                    _protocols.Add($"git@{serverName}:{route}");

                    SetCurrentValue(ActiveProtocolProperty, "HTTPS");
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

                    SetCurrentValue(ActiveProtocolProperty, "SSH");
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
        private List<string> _protocols = [];
    }
}
