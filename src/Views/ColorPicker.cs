using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace SourceGit.Views
{
    public class ColorPicker : Control
    {
        public static readonly StyledProperty<uint> ValueProperty =
            AvaloniaProperty.Register<ColorPicker, uint>(nameof(Value));

        public uint Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        // Values are copied from Avalonia: src/Avalonia.Controls.ColorPicker/ColorPalettes/FluentColorPalette.cs
        private static readonly Color[,] COLOR_TABLE = new[,]
        {
            {
                Color.FromArgb(255, 255,  67,  67), /* #FF4343 */
                Color.FromArgb(255, 209,  52,  56), /* #D13438 */
                Color.FromArgb(255, 239, 105,  80), /* #EF6950 */
                Color.FromArgb(255, 218,  59,   1), /* #DA3B01 */
                Color.FromArgb(255, 202,  80,  16), /* #CA5010 */
                Color.FromArgb(255, 247,  99,  12), /* #F7630C */
                Color.FromArgb(255, 255, 140,   0), /* #FF8C00 */
                Color.FromArgb(255, 255, 185,   0), /* #FFB900 */
            },
            {
                Color.FromArgb(255, 231,  72,  86), /* #E74856 */
                Color.FromArgb(255, 232,  17,  35), /* #E81123 */
                Color.FromArgb(255, 234,   0,  94), /* #EA005E */
                Color.FromArgb(255, 195,   0,  82), /* #C30052 */
                Color.FromArgb(255, 227,   0, 140), /* #E3008C */
                Color.FromArgb(255, 191,   0, 119), /* #BF0077 */
                Color.FromArgb(255, 194,  57, 179), /* #C239B3 */
                Color.FromArgb(255, 154,   0, 137), /* #9A0089 */
            },
            {
                Color.FromArgb(255,   0, 120, 215), /* #0078D7 */
                Color.FromArgb(255,   0,  99, 177), /* #0063B1 */
                Color.FromArgb(255, 142, 140, 216), /* #8E8CD8 */
                Color.FromArgb(255, 107, 105, 214), /* #6B69D6 */
                Color.FromArgb(255, 135, 100, 184), /* #8764B8 */
                Color.FromArgb(255, 116,  77, 169), /* #744DA9 */
                Color.FromArgb(255, 177,  70, 194), /* #B146C2 */
                Color.FromArgb(255, 136,  23, 152), /* #881798 */
            },
            {
                Color.FromArgb(255,   0, 153, 188), /* #0099BC */
                Color.FromArgb(255,  45, 125, 154), /* #2D7D9A */
                Color.FromArgb(255,   0, 183, 195), /* #00B7C3 */
                Color.FromArgb(255,   3, 131, 135), /* #038387 */
                Color.FromArgb(255,   0, 178, 148), /* #00B294 */
                Color.FromArgb(255,   1, 133, 116), /* #018574 */
                Color.FromArgb(255,   0, 204, 106), /* #00CC6A */
                Color.FromArgb(255,  16, 137,  62), /* #10893E */
            },
            {
                Color.FromArgb(255, 122, 117, 116), /* #7A7574 */
                Color.FromArgb(255,  93,  90,  80), /* #5D5A58 */
                Color.FromArgb(255, 104, 118, 138), /* #68768A */
                Color.FromArgb(255,  81,  92, 107), /* #515C6B */
                Color.FromArgb(255,  86, 124, 115), /* #567C73 */
                Color.FromArgb(255,  72, 104,  96), /* #486860 */
                Color.FromArgb(255,  73, 130,   5), /* #498205 */
                Color.FromArgb(255,  16, 124,  16), /* #107C10 */
            },
            {
                Color.FromArgb(255, 118, 118, 118), /* #767676 */
                Color.FromArgb(255,  76,  74,  72), /* #4C4A48 */
                Color.FromArgb(255, 105, 121, 126), /* #69797E */
                Color.FromArgb(255,  74,  84,  89), /* #4A5459 */
                Color.FromArgb(255, 100, 124, 100), /* #647C64 */
                Color.FromArgb(255,  82,  94,  84), /* #525E54 */
                Color.FromArgb(255, 132, 117,  69), /* #847545 */
                Color.FromArgb(255, 126, 115,  95), /* #7E735F */
            }
        };

        static ColorPicker()
        {
            ValueProperty.Changed.AddClassHandler<ColorPicker>((c, _) => c.UpdateColors());
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            // Color table.
            {
                // Colors
                for (int i = 0; i < 6; i++)
                {
                    for (int j = 0; j < 8; j++)
                        context.FillRectangle(new SolidColorBrush(COLOR_TABLE[i, j]), new Rect(j * 32, i * 32, 32, 32));
                }

                // Borders
                var border = this.FindResource("Brush.Border0") as IBrush;
                var pen = new Pen(border, 0.4);
                for (int i = 1; i < 6; i++)
                    context.DrawLine(pen, new Point(0, i * 32), new Point(256, i * 32));
                for (int j = 1; j < 8; j++)
                    context.DrawLine(pen, new Point(j * 32, 0), new Point(j * 32, 192));

                // Selected
                if (_highlightedTableRect is { } rect)
                    context.DrawRectangle(new Pen(Brushes.White, 2), rect);
            }

            // Palette picker
            {
                context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 200, 256, 32), 4, 4, _shadow);

                context.DrawRectangle(new SolidColorBrush(_darkestColor), null, _darkestRect);
                context.FillRectangle(new SolidColorBrush(_darkerColor), _darkerRect);
                context.FillRectangle(new SolidColorBrush(_darkColor), _darkRect);
                context.FillRectangle(new SolidColorBrush(_lightColor), _lightRect);
                context.FillRectangle(new SolidColorBrush(_lighterColor), _lighterRect);
                context.DrawRectangle(new SolidColorBrush(_lightestColor), null, _lightestRect);

                context.DrawRectangle(new SolidColorBrush(_color), null, new Rect(96, 200 - 4, 64, 40), 4, 4, _shadow);
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ValueProperty)
            {
                UpdateColors();
                InvalidateVisual();
            }
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            _highlightedTableRect = null;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(256, 240);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            var p = e.GetPosition(this);
            if (_colorTableRect.Contains(p))
            {
                var col = (int)Math.Floor(p.X / 32.0);
                var row = (int)Math.Floor(p.Y / 32.0);
                var rect = new Rect(col * 32 + 2, row * 32 + 2, 28, 28);
                if (!rect.Equals(_highlightedTableRect))
                {
                    _highlightedTableRect = rect;
                    SetCurrentValue(ValueProperty, COLOR_TABLE[row, col].ToUInt32());
                }

                return;
            }

            if (_darkestRect.Rect.Contains(p))
            {
                _highlightedTableRect = null;
                SetCurrentValue(ValueProperty, _darkestColor.ToUInt32());
            }
            else if (_darkerRect.Contains(p))
            {
                _highlightedTableRect = null;
                SetCurrentValue(ValueProperty, _darkerColor.ToUInt32());
            }
            else if (_darkRect.Contains(p))
            {
                _highlightedTableRect = null;
                SetCurrentValue(ValueProperty, _darkColor.ToUInt32());
            }
            else if (_lightRect.Contains(p))
            {
                _highlightedTableRect = null;
                SetCurrentValue(ValueProperty, _lightColor.ToUInt32());
            }
            else if (_lighterRect.Contains(p))
            {
                _highlightedTableRect = null;
                SetCurrentValue(ValueProperty, _lighterColor.ToUInt32());
            }
            else if (_lightestRect.Rect.Contains(p))
            {
                _highlightedTableRect = null;
                SetCurrentValue(ValueProperty, _lightestColor.ToUInt32());
            }
        }

        private void UpdateColors()
        {
            _color = Color.FromUInt32(Value);

            var hsvColor = _color.ToHsv();
            _darkestColor = GetNextColor(hsvColor, -0.3);
            _darkerColor = GetNextColor(hsvColor, -0.2);
            _darkColor = GetNextColor(hsvColor, -0.1);
            _lightColor = GetNextColor(hsvColor, 0.1);
            _lighterColor = GetNextColor(hsvColor, 0.2);
            _lightestColor = GetNextColor(hsvColor, 0.3);
        }

        private Color GetNextColor(HsvColor c, double step)
        {
            var v = c.V;
            v += step;
            v = Math.Round(v, 2);

            var newColor = new HsvColor(c.A, c.H, c.S, v);
            return newColor.ToRgb();
        }

        private BoxShadows _shadow = BoxShadows.Parse("0 0 6 0 #A9000000");

        private Rect _colorTableRect = new Rect(0, 0, 32 * 8, 32 * 6);
        private RoundedRect _darkestRect = new RoundedRect(new Rect(0, 200, 32, 32), new CornerRadius(4, 0, 0, 4));
        private Rect _darkerRect = new Rect(32, 200, 32, 32);
        private Rect _darkRect = new Rect(64, 200, 32, 32);
        private Rect _lightRect = new Rect(160, 200, 32, 32);
        private Rect _lighterRect = new Rect(192, 200, 32, 32);
        private RoundedRect _lightestRect = new RoundedRect(new Rect(224, 200, 32, 32), new CornerRadius(0, 4, 4, 0));
        private Rect? _highlightedTableRect = null;

        private Color _darkestColor;
        private Color _darkerColor;
        private Color _darkColor;
        private Color _color;
        private Color _lightColor;
        private Color _lighterColor;
        private Color _lightestColor;
    }
}
