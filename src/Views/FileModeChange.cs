using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SourceGit.Views
{
    public class FileModeChange : Control
    {
        public static readonly DirectProperty<FileModeChange, int> OldModeProperty =
            AvaloniaProperty.RegisterDirect<FileModeChange, int>(
                nameof(OldMode),
                static o => o.OldMode,
                static (o, v) => o.OldMode = v);

        public int OldMode
        {
            get => _oldMode;
            set => SetAndRaise(OldModeProperty, ref _oldMode, value);
        }

        public static readonly DirectProperty<FileModeChange, int> NewModeProperty =
            AvaloniaProperty.RegisterDirect<FileModeChange, int>(
                nameof(NewMode),
                static o => o.NewMode,
                static (o, v) => o.NewMode = v);

        public int NewMode
        {
            get => _newMode;
            set => SetAndRaise(NewModeProperty, ref _newMode, value);
        }

        public static readonly StyledProperty<FontFamily> FontFamilyProperty =
            TextBlock.FontFamilyProperty.AddOwner<FileModeChange>();

        public FontFamily FontFamily
        {
            get => GetValue(FontFamilyProperty);
            set => SetValue(FontFamilyProperty, value);
        }

        public static readonly StyledProperty<double> FontSizeProperty =
           TextBlock.FontSizeProperty.AddOwner<FileModeChange>();

        public double FontSize
        {
            get => GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public static readonly StyledProperty<IBrush> ForegroundProperty =
            TextBlock.ForegroundProperty.AddOwner<FileModeChange>();

        public IBrush Foreground
        {
            get => GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public static readonly StyledProperty<IBrush> BackgroundProperty =
            TextBlock.BackgroundProperty.AddOwner<FileModeChange>();

        public IBrush Background
        {
            get => GetValue(BackgroundProperty);
            set => SetValue(BackgroundProperty, value);
        }

        public override void Render(DrawingContext context)
        {
            if (_label == null)
                return;

            var rect = new Rect(4, 0, _label.WidthIncludingTrailingWhitespace + 8, Bounds.Height);
            context.FillRectangle(Background, rect, 4);
            context.DrawText(_label, new Point(8, 9 - _label.Height * 0.5));
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == OldModeProperty ||
                change.Property == NewModeProperty ||
                change.Property == FontFamilyProperty ||
                change.Property == ForegroundProperty)
                InvalidateMeasure();
            else if (change.Property == BackgroundProperty)
                InvalidateVisual();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (_oldMode == 0 && _newMode == 0)
            {
                _label = null;
                ToolTip.SetTip(this, null);
                return new Size(0, 0);
            }

            _label = new FormattedText(
                $"{_oldMode} -> {_newMode}",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily),
                FontSize,
                Foreground);

            if (_oldMode == 0)
                ToolTip.SetTip(this, $"{App.Text("FileModeChange.New")}{TranslateMode(_newMode)}");
            else if (_newMode == 0)
                ToolTip.SetTip(this, $"{App.Text("FileModeChange.Deleted")}{TranslateMode(_oldMode)}");
            else
                ToolTip.SetTip(this, $"{App.Text("FileModeChange")}{TranslateMode(_oldMode)} -> {TranslateMode(_newMode)}");

            return new Size(_label.WidthIncludingTrailingWhitespace + 12, 18);
        }

        private string TranslateMode(int mode)
        {
            var key = mode switch
            {
                100644 => "FileModeChange.Normal",
                100755 => "FileModeChange.Executable",
                040000 => "FileModeChange.Directory",
                120000 => "FileModeChange.Symlink",
                160000 => "FileModeChange.Submodule",
                _ => "FileModeChange.Unknown"
            };

            return App.Text(key);
        }

        private int _oldMode = 0;
        private int _newMode = 0;
        private FormattedText _label = null;
    }
}
