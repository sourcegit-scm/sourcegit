using System;
using System.Collections.Generic;
using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public record PieChartToolTip(string Title, int Count, double Percentage, IBrush Brush);

    public class PieChart : Control
    {
        public static readonly DirectProperty<PieChart, Models.StatisticsReport> ReportProperty =
            AvaloniaProperty.RegisterDirect<PieChart, Models.StatisticsReport>(
                nameof(Report),
                o => o.Report,
                (o, v) => o.Report = v);

        public Models.StatisticsReport Report
        {
            get => _report;
            set => SetAndRaise(ReportProperty, ref _report, value);
        }

        public static readonly DirectProperty<PieChart, PieChartToolTip> HoveringTipProperty =
            AvaloniaProperty.RegisterDirect<PieChart, PieChartToolTip>(
                nameof(HoveringTip),
                o => o.HoveringTip);

        public PieChartToolTip HoveringTip
        {
            get => _hoveringTip;
            private set => SetAndRaise(HoveringTipProperty, ref _hoveringTip, value);
        }

        public static readonly StyledProperty<FontFamily> FontFamilyProperty =
            AvaloniaProperty.Register<PieChart, FontFamily>(nameof(FontFamily));

        public FontFamily FontFamily
        {
            get => GetValue(FontFamilyProperty);
            set => SetValue(FontFamilyProperty, value);
        }

        public static readonly StyledProperty<FontFamily> SecondaryFontFamilyProperty =
            AvaloniaProperty.Register<PieChart, FontFamily>(nameof(SecondaryFontFamily));

        public FontFamily SecondaryFontFamily
        {
            get => GetValue(SecondaryFontFamilyProperty);
            set => SetValue(SecondaryFontFamilyProperty, value);
        }

        public static readonly StyledProperty<IBrush> ForegroundProperty =
            AvaloniaProperty.Register<PieChart, IBrush>(nameof(Foreground), Brushes.White);

        public IBrush Foreground
        {
            get => GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public static readonly StyledProperty<IBrush> SecondaryForegroundProperty =
            AvaloniaProperty.Register<PieChart, IBrush>(nameof(SecondaryForeground), Brushes.Gray);

        public IBrush SecondaryForeground
        {
            get => GetValue(SecondaryForegroundProperty);
            set => SetValue(SecondaryForegroundProperty, value);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            _arcs.Clear();
            _lastHovered = null;

            var w = Bounds.Width;
            var h = Bounds.Height;
            var foreground = Foreground;
            var secondaryForeground = SecondaryForeground;
            var typeface = new Typeface(FontFamily);
            var secondaryTypeface = new Typeface(SecondaryFontFamily);

            _center = new Point(w / 2, h / 2);
            _radius = Math.Min(w, h) / 2 - 48;
            context.FillRectangle(Brushes.Transparent, new Rect(0, 0, w, h));

            if (_report == null || _report.Authors.Count == 0)
            {
                var label = new FormattedText("0", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 14, foreground);
                context.DrawEllipse(null, new Pen(s_brushes[0]), _center, _radius, _radius);
                context.DrawText(label, new Point(_center.X - label.Width / 2, _center.Y - label.Height / 2));
                return;
            }
            else if (_report.Authors.Count == 1)
            {
                var single = _report.Authors[0];
                var label = new FormattedText(
                    single.User.Name,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    12,
                    Brushes.White);
                context.DrawEllipse(s_brushes[0], null, _center, _radius, _radius);
                context.DrawText(label, new Point(_center.X - label.Width / 2, _center.Y - label.Height / 2));

                _arcs.Add(new(0, 2 * Math.PI, single.User.Name, single.Count, 1.0, s_brushes[0], single));
                return;
            }

            var total = _report.Total;
            var renderedCount = 0;
            var startAngle = -Math.PI / 2;
            var remaining = 2 * Math.PI;
            var leftArcs = new List<Arc>();
            var rightArcs = new List<Arc>();
            for (var i = 0; i < _report.Authors.Count; i++)
            {
                var author = _report.Authors[i];
                var percent = (double)author.Count / total;
                if (percent > 0.01)
                {
                    var brushIdx = i % s_brushes.Length;
                    var brush = (i == _report.Authors.Count - 1 && brushIdx == 0) ? s_brushes[1] : s_brushes[brushIdx];
                    var sweepAngle = percent * 2 * Math.PI;
                    DrawArc(context, startAngle, sweepAngle, brush);

                    var arc = new Arc(startAngle, sweepAngle, author.User.Name, author.Count, percent, brush, author);
                    _arcs.Add(arc);
                    if (arc.IsLeftSide)
                        leftArcs.Add(arc);
                    else
                        rightArcs.Add(arc);

                    remaining -= sweepAngle;
                    startAngle += sweepAngle;
                    renderedCount += author.Count;
                }
                else
                {
                    var sweepAngle = remaining;
                    var count = total - renderedCount;
                    DrawArc(context, startAngle, sweepAngle, Brushes.Gray);

                    var arc = new Arc(startAngle, sweepAngle, "Others", count, (double)count / total, Brushes.Gray, null);
                    _arcs.Add(arc);
                    if (arc.IsLeftSide)
                        leftArcs.Add(arc);
                    else
                        rightArcs.Add(arc);
                    break;
                }
            }

            if (leftArcs.Count > 0)
            {
                for (var i = leftArcs.Count - 1; i >= 0; i--)
                {
                    var arc = leftArcs[i];
                    var pen = new Pen(arc.Tip.Brush, 1);
                    var lineStartX = _center.X - _radius - 32;

                    var edgePoint = new Point(
                        _center.X + _radius * Math.Cos(arc.MidAngle),
                        _center.Y + _radius * Math.Sin(arc.MidAngle));

                    var edgeLineStartPoint = new Point(
                        _center.X + (_radius + 32) * Math.Cos(arc.MidAngle),
                        _center.Y + (_radius + 32) * Math.Sin(arc.MidAngle));

                    context.DrawLine(pen, new Point(lineStartX, edgeLineStartPoint.Y), edgeLineStartPoint);
                    context.DrawLine(pen, edgeLineStartPoint, edgePoint);

                    var title = new FormattedText(
                        arc.Tip.Title,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        12,
                        foreground);
                    var percentage = new FormattedText(
                        $"{arc.Tip.Percentage:P1}",
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        secondaryTypeface,
                        12,
                        secondaryForeground);

                    var labelStartX = lineStartX - 8 - percentage.WidthIncludingTrailingWhitespace - 4 - title.WidthIncludingTrailingWhitespace;
                    var labelMidY = edgeLineStartPoint.Y;
                    context.DrawText(title, new Point(labelStartX, labelMidY - title.Height * 0.5));
                    context.DrawText(percentage, new Point(labelStartX + title.WidthIncludingTrailingWhitespace + 4, labelMidY - percentage.Height * 0.5));
                }
            }

            if (rightArcs.Count > 0)
            {
                for (var i = 0; i < rightArcs.Count; i++)
                {
                    var arc = rightArcs[i];
                    var pen = new Pen(arc.Tip.Brush, 1);
                    var lineStartX = _center.X + _radius + 32;

                    var edgePoint = new Point(
                        _center.X + _radius * Math.Cos(arc.MidAngle),
                        _center.Y + _radius * Math.Sin(arc.MidAngle));

                    var edgeLineStartPoint = new Point(
                        _center.X + (_radius + 32) * Math.Cos(arc.MidAngle),
                        _center.Y + (_radius + 32) * Math.Sin(arc.MidAngle));

                    context.DrawLine(pen, new Point(lineStartX, edgeLineStartPoint.Y), edgeLineStartPoint);
                    context.DrawLine(pen, edgeLineStartPoint, edgePoint);

                    var title = new FormattedText(
                        arc.Tip.Title,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        12,
                        foreground);
                    var percentage = new FormattedText(
                        $"{arc.Tip.Percentage:P1}",
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        secondaryTypeface,
                        12,
                        secondaryForeground);

                    var labelStartX = lineStartX + 8;
                    var labelMidY = edgeLineStartPoint.Y;
                    context.DrawText(title, new Point(labelStartX, labelMidY - title.Height * 0.5));
                    context.DrawText(percentage, new Point(labelStartX + title.WidthIncludingTrailingWhitespace + 4, labelMidY - percentage.Height * 0.5));
                }
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ReportProperty)
            {
                _lastHovered = null;
                HoveringTip = null;
            }

            if (change.Property == ReportProperty ||
                change.Property == FontFamilyProperty ||
                change.Property == ForegroundProperty ||
                change.Property == SecondaryForegroundProperty)
                InvalidateVisual();
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            if (_report == null || _report.Authors.Count == 0)
            {
                _lastHovered = null;
                HoveringTip = null;
                return;
            }

            var pos = e.GetPosition(this);
            var distance = Math.Sqrt(Math.Pow(pos.X - _center.X, 2) + Math.Pow(pos.Y - _center.Y, 2));
            if (distance > _radius)
            {
                _lastHovered = null;
                HoveringTip = null;
                return;
            }

            var angle = Math.Atan2(pos.Y - _center.Y, pos.X - _center.X);
            if (angle < -Math.PI / 2)
                angle += 2 * Math.PI;

            foreach (var arc in _arcs)
            {
                if (angle >= arc.StartAngle && angle < arc.EndAngle)
                {
                    if (_lastHovered == arc)
                        return;

                    _lastHovered = arc;
                    HoveringTip = arc.Tip;
                    return;
                }
            }

            _lastHovered = null;
            HoveringTip = null;
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (_lastHovered != null)
            {
                var statisticsView = this.FindAncestorOfType<Statistics>();
                if (statisticsView is { DataContext: ViewModels.Statistics vm })
                {
                    if (_lastHovered.Author == null || _lastHovered.Author == vm.SelectedAuthor)
                        vm.ChangeAuthor(null);
                    else
                        vm.ChangeAuthor(_lastHovered.Author);
                }
            }
        }

        private void DrawArc(DrawingContext context, double startAngle, double sweepAngle, IBrush brush)
        {
            var endAngle = startAngle + sweepAngle;
            var startPoint = new Point(_center.X + _radius * Math.Cos(startAngle), _center.Y + _radius * Math.Sin(startAngle));
            var endPoint = new Point(_center.X + _radius * Math.Cos(endAngle), _center.Y + _radius * Math.Sin(endAngle));
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(_center, true);
                ctx.LineTo(startPoint);
                ctx.ArcTo(endPoint, new Size(_radius, _radius), 0, sweepAngle > Math.PI, SweepDirection.Clockwise);
                ctx.LineTo(_center);
            }
            context.DrawGeometry(brush, null, geometry);
        }

        private class Arc
        {
            public double StartAngle { get; set; }
            public double EndAngle { get; set; }
            public PieChartToolTip Tip { get; set; }
            public Models.StatisticsAuthor Author { get; set; }

            public double MidAngle => (StartAngle + EndAngle) / 2;
            public bool IsLeftSide => MidAngle > Math.PI * 0.5;

            public Arc(double startAngle, double sweepAngle, string title, int count, double percentage, IBrush brush, Models.StatisticsAuthor author)
            {
                StartAngle = startAngle;
                EndAngle = startAngle + sweepAngle;
                Tip = new PieChartToolTip(title, count, percentage, brush);
                Author = author;
            }
        }

        private static readonly IBrush[] s_brushes = new IBrush[]
        {
            Brushes.Orange,
            Brushes.ForestGreen,
            Brushes.Turquoise,
            Brushes.Olive,
            Brushes.Magenta,
            Brushes.Red,
            Brushes.Khaki,
            Brushes.Lime,
            Brushes.RoyalBlue,
            Brushes.Teal,
        };

        private Models.StatisticsReport _report = null;
        private Point _center = new Point(0, 0);
        private double _radius = 0;
        private List<Arc> _arcs = new List<Arc>();
        private Arc _lastHovered = null;
        private PieChartToolTip _hoveringTip = null;
    }
}
