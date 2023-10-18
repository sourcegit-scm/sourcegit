using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace SourceGit.Views.Controls {
    /// <summary>
    ///     变更状态图标
    /// </summary>
    class ChangeStatusIcon : FrameworkElement {
        public static readonly Brush[] Backgrounds = new Brush[] {
            Brushes.Transparent,
            new LinearGradientBrush(Color.FromRgb(238, 160, 14), Color.FromRgb(228, 172, 67), 90),
            new LinearGradientBrush(Color.FromRgb(47, 185, 47), Color.FromRgb(75, 189, 75), 90),
            new LinearGradientBrush(Colors.Tomato, Color.FromRgb(252, 165, 150), 90),
            new LinearGradientBrush(Colors.Orchid, Color.FromRgb(248, 161, 245), 90),
            new LinearGradientBrush(Color.FromRgb(238, 160, 14), Color.FromRgb(228, 172, 67), 90),
            new LinearGradientBrush(Color.FromRgb(238, 160, 14), Color.FromRgb(228, 172, 67), 90),
            new LinearGradientBrush(Color.FromRgb(47, 185, 47), Color.FromRgb(75, 189, 75), 90),
        };

        public static readonly string[] Labels = new string[] {
            "?",
            "±",
            "+",
            "−",
            "➜",
            "❏",
            "U",
            "★",
        };

        public static readonly DependencyProperty ChangeProperty = DependencyProperty.Register(
            "Change",
            typeof(Models.Change),
            typeof(ChangeStatusIcon),
            new PropertyMetadata(null, ForceDirty));

        public Models.Change Change {
            get { return (Models.Change)GetValue(ChangeProperty); }
            set { SetValue(ChangeProperty, value); }
        }

        public static readonly DependencyProperty IsLocalChangeProperty = DependencyProperty.Register(
            "IsLocalChange",
            typeof(bool),
            typeof(ChangeStatusIcon),
            new PropertyMetadata(false, ForceDirty));

        public bool IsLocalChange {
            get { return (bool)GetValue(IsLocalChangeProperty); }
            set { SetValue(IsLocalChangeProperty, value); }
        }

        private Brush background;
        private FormattedText label;

        public ChangeStatusIcon() {
            HorizontalAlignment = HorizontalAlignment.Center;
            VerticalAlignment = VerticalAlignment.Center;
        }

        protected override void OnRender(DrawingContext dc) {
            if (background == null || label == null) return;
            var corner = Math.Max(2, Width / 16);
            dc.DrawRoundedRectangle(background, null, new Rect(0, 0, Width, Height), corner, corner);
            dc.DrawText(label, new Point((Width - label.Width) * 0.5, 0));
        }

        private static void ForceDirty(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var icon = d as ChangeStatusIcon;
            if (icon == null) return;

            if (icon.Change == null) {
                icon.background = null;
                icon.label = null;
                return;
            }

            string txt;
            if (icon.IsLocalChange) {
                if (icon.Change.IsConflit) {
                    icon.background = Brushes.OrangeRed;
                    txt = "!";
                } else {
                    icon.background = Backgrounds[(int)icon.Change.WorkTree];
                    txt = Labels[(int)icon.Change.WorkTree];
                }
            } else {
                icon.background = Backgrounds[(int)icon.Change.Index];
                txt = Labels[(int)icon.Change.Index];
            }

            icon.label = new FormattedText(
                txt,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily(Models.Preference.Instance.General.FontFamilyWindow), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                icon.Width * 0.8,
                new SolidColorBrush(Color.FromRgb(241, 241, 241)),
                VisualTreeHelper.GetDpi(icon).PixelsPerDip);

            icon.InvalidateVisual();
        }
    }
}
