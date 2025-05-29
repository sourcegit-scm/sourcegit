using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SourceGit.Views
{
    public class ChangeStatusIcon : Control
    {
        private static readonly Dictionary<Models.ChangeState, IBrush> BACKGROUNDS = new Dictionary<Models.ChangeState, IBrush>()
        {
            { Models.ChangeState.None, Brushes.Transparent },
            { Models.ChangeState.Modified, new LinearGradientBrush
                {
                    GradientStops = new GradientStops() { new GradientStop(Color.FromRgb(238, 160, 14), 0), new GradientStop(Color.FromRgb(228, 172, 67), 1) },
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                }
            },
            { Models.ChangeState.TypeChanged, new LinearGradientBrush
                {
                    GradientStops = new GradientStops() { new GradientStop(Color.FromRgb(238, 160, 14), 0), new GradientStop(Color.FromRgb(228, 172, 67), 1) },
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                }
            },
            { Models.ChangeState.Added, new LinearGradientBrush
                {
                    GradientStops = new GradientStops() { new GradientStop(Color.FromRgb(47, 185, 47), 0), new GradientStop(Color.FromRgb(75, 189, 75), 1) },
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                }
            },
            { Models.ChangeState.Deleted, new LinearGradientBrush
                {
                    GradientStops = new GradientStops() { new GradientStop(Colors.Tomato, 0), new GradientStop(Color.FromRgb(252, 165, 150), 1) },
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                }
            },
            { Models.ChangeState.Renamed, new LinearGradientBrush
                {
                    GradientStops = new GradientStops() { new GradientStop(Colors.Orchid, 0), new GradientStop(Color.FromRgb(248, 161, 245), 1) },
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                }
            },
            { Models.ChangeState.Copied, new LinearGradientBrush
                {
                    GradientStops = new GradientStops() { new GradientStop(Color.FromRgb(238, 160, 14), 0), new GradientStop(Color.FromRgb(228, 172, 67), 1) },
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                }
            },
            { Models.ChangeState.Untracked, new LinearGradientBrush
                {
                    GradientStops = new GradientStops() { new GradientStop(Color.FromRgb(47, 185, 47), 0), new GradientStop(Color.FromRgb(75, 189, 75), 1) },
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                }
            },
            { Models.ChangeState.Conflicted, Brushes.OrangeRed },
        };

        private static readonly Dictionary<Models.ChangeState, string> INDICATOR = new Dictionary<Models.ChangeState, string>()
        {
            { Models.ChangeState.None, "?" },
            { Models.ChangeState.Modified, "±" },
            { Models.ChangeState.TypeChanged, "T" },
            { Models.ChangeState.Added, "+" },
            { Models.ChangeState.Deleted, "−" },
            { Models.ChangeState.Renamed, "➜" },
            { Models.ChangeState.Copied, "❏" },
            { Models.ChangeState.Untracked, "★" },
            { Models.ChangeState.Conflicted, "!" }
        };

        private static readonly Dictionary<Models.ChangeState, string> TIPS = new Dictionary<Models.ChangeState, string>()
        {
            { Models.ChangeState.None, "Unknown" },
            { Models.ChangeState.Modified, "Modified" },
            { Models.ChangeState.TypeChanged, "Type Changed" },
            { Models.ChangeState.Added, "Added" },
            { Models.ChangeState.Deleted, "Deleted" },
            { Models.ChangeState.Renamed, "Renamed" },
            { Models.ChangeState.Copied, "Copied" },
            { Models.ChangeState.Untracked, "Untracked" },
            { Models.ChangeState.Conflicted, "Conflict" }
        };

        public static readonly StyledProperty<bool> IsUnstagedChangeProperty =
            AvaloniaProperty.Register<ChangeStatusIcon, bool>(nameof(IsUnstagedChange));

        public bool IsUnstagedChange
        {
            get => GetValue(IsUnstagedChangeProperty);
            set => SetValue(IsUnstagedChangeProperty, value);
        }

        public static readonly StyledProperty<Models.Change> ChangeProperty =
            AvaloniaProperty.Register<ChangeStatusIcon, Models.Change>(nameof(Change));

        public Models.Change Change
        {
            get => GetValue(ChangeProperty);
            set => SetValue(ChangeProperty, value);
        }

        public override void Render(DrawingContext context)
        {
            if (Change == null || Bounds.Width <= 0)
                return;

            var typeface = new Typeface("fonts:SourceGit#JetBrains Mono");

            IBrush background;
            string indicator;
            if (IsUnstagedChange)
            {
                var status = Models.Change.GetPrimaryState(Change.WorkTree);
                background = BACKGROUNDS[status];
                indicator = INDICATOR[status];
            }
            else
            {
                var status = Models.Change.GetPrimaryState(Change.Index);
                background = BACKGROUNDS[status];
                indicator = INDICATOR[status];
            }

            var txt = new FormattedText(
                indicator,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                Bounds.Width * 0.8,
                Brushes.White);

            var corner = (float)Math.Max(2, Bounds.Width / 16);
            var textOrigin = new Point((Bounds.Width - txt.Width) * 0.5, (Bounds.Height - txt.Height) * 0.5);
            context.DrawRectangle(background, null, new Rect(0, 0, Bounds.Width, Bounds.Height), corner, corner);
            context.DrawText(txt, textOrigin);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == IsUnstagedChangeProperty || change.Property == ChangeProperty)
            {
                var isUnstaged = IsUnstagedChange;
                var c = Change;
                if (c == null)
                {
                    ToolTip.SetTip(this, null);
                    return;
                }

                var status = isUnstaged ?
                    Models.Change.GetPrimaryState(c.WorkTree) :
                    Models.Change.GetPrimaryState(c.Index);

                ToolTip.SetTip(this, TIPS[status]);
                InvalidateVisual();
            }
        }
    }
}
