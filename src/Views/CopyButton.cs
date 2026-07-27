using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace SourceGit.Views
{
    public class CopyButton : Button
    {
        protected override Type StyleKeyOverride => typeof(Button);

        public static readonly StyledProperty<string> CopyTextProperty =
            AvaloniaProperty.Register<CopyButton, string>(nameof(CopyText), string.Empty);

        public string CopyText
        {
            get => GetValue(CopyTextProperty);
            set => SetValue(CopyTextProperty, value);
        }

        public static readonly DirectProperty<CopyButton, bool> IsCopiedProperty =
            AvaloniaProperty.RegisterDirect<CopyButton, bool>(
                nameof(IsCopied),
                static o => o.IsCopied);

        public bool IsCopied
        {
            get => _isCopied;
            private set => SetAndRaise(IsCopiedProperty, ref _isCopied, value);
        }

        public CopyButton()
        {
            Classes.Add("icon_button");

            _copyIcon = new Path() { Width = 12, Height = 12 };
            _checkIcon = new Path()
            {
                Width = 14,
                Height = 14,
                Margin = new Thickness(0, 2, 0, 0),
                Fill = Brushes.Green,
                IsVisible = false,
            };

            var grid = new Grid();
            grid.Children.Add(_copyIcon);
            grid.Children.Add(_checkIcon);
            Content = grid;
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            if (this.FindResource("Icons.Copy") is Geometry copyGeo)
                _copyIcon.Data = copyGeo;
            if (this.FindResource("Icons.Check") is Geometry checkGeo)
                _checkIcon.Data = checkGeo;

            _resetTimer = new DispatcherTimer();
            _resetTimer.Interval = TimeSpan.FromSeconds(1);
            _resetTimer.Tag = this;
            _resetTimer.Tick += static (o, _) =>
            {
                if (o is DispatcherTimer { Tag: CopyButton btn } timer)
                {
                    btn.IsCopied = false;
                    timer.IsEnabled = false;
                }
            };
            _resetTimer.IsEnabled = false;
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            if (_resetTimer != null)
            {
                _resetTimer.Tag = null;
                _resetTimer.IsEnabled = false;
            }

            base.OnUnloaded(e);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == IsCopiedProperty)
            {
                _copyIcon.IsVisible = !_isCopied;
                _checkIcon.IsVisible = _isCopied;
            }
            else if (change.Property == CopyTextProperty)
            {
                // Reset the copied state when CopyText changes (e.g. switching to a different commit)
                IsCopied = false;
                _resetTimer?.Stop();
            }
        }

        protected override async void OnClick()
        {
            base.OnClick();

            var text = CopyText;
            if (!string.IsNullOrEmpty(text))
                await this.CopyTextAsync(text);

            IsCopied = true;
            _resetTimer?.Start();
        }

        private readonly Path _copyIcon;
        private readonly Path _checkIcon;
        private bool _isCopied = false;
        private DispatcherTimer _resetTimer = null;
    }
}
