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

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            _arcs.Clear();
            _lastHovered = null;

            var w = Bounds.Width;
            var h = Bounds.Height;
            var foreground = Foreground;
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

                _arcs.Add(new(0, 2 * Math.PI, single.User.Name, single.Count, 1.0, s_brushes[0], single, _center));
                return;
            }

            var total = _report.Total;
            var renderedCount = 0;
            var startAngle = -Math.PI / 2;
            var remaining = 2 * Math.PI;
            var labelRender = new LabelRender();
            for (var i = 0; i < _report.Authors.Count; i++)
            {
                var author = _report.Authors[i];
                var percent = (double)author.Count / total;
                if (percent > 0.01)
                {
                    var brushIdx = i % s_brushes.Length;
                    var brush = (i == _report.Authors.Count - 1 && brushIdx == 0) ? s_brushes[1] : s_brushes[brushIdx];
                    var sweepAngle = percent * 2 * Math.PI;
                    var labelAlignPoint = DrawArc(context, startAngle, sweepAngle, brush);
                    var arc = new Arc(startAngle, sweepAngle, author.User.Name, author.Count, percent, brush, author, labelAlignPoint);
                    _arcs.Add(arc);
                    labelRender.Append(arc, typeface, secondaryTypeface, foreground);

                    remaining -= sweepAngle;
                    startAngle += sweepAngle;
                    renderedCount += author.Count;
                }
                else
                {
                    var sweepAngle = remaining;
                    var count = total - renderedCount;
                    var labelAlignPoint = DrawArc(context, startAngle, sweepAngle, Brushes.Gray);
                    var arc = new Arc(startAngle, sweepAngle, "Others", count, (double)count / total, Brushes.Gray, null, labelAlignPoint);
                    _arcs.Add(arc);
                    labelRender.Append(arc, typeface, secondaryTypeface, foreground);
                    break;
                }
            }

            labelRender.Draw(context);
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
                change.Property == SecondaryFontFamilyProperty ||
                change.Property == ForegroundProperty)
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

        private Point DrawArc(DrawingContext context, double startAngle, double sweepAngle, IBrush brush)
        {
            var endAngle = startAngle + sweepAngle;
            var midAngle = (startAngle + endAngle) / 2;
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

            var pen = new Pen(brush, 1);
            var lineStartX = midAngle > Math.PI * 0.5 ? _center.X - _radius - 32 : _center.X + _radius + 32;

            var edgePoint = new Point(
                _center.X + _radius * Math.Cos(midAngle),
                _center.Y + _radius * Math.Sin(midAngle));

            var edgeLineStartPoint = new Point(
                _center.X + (_radius + 32) * Math.Cos(midAngle),
                _center.Y + (_radius + 32) * Math.Sin(midAngle));

            context.DrawLine(pen, new Point(lineStartX, edgeLineStartPoint.Y), edgeLineStartPoint);
            context.DrawLine(pen, edgeLineStartPoint, edgePoint);
            return new Point(lineStartX, edgeLineStartPoint.Y);
        }

        private class Arc
        {
            public double StartAngle { get; set; }
            public double EndAngle { get; set; }
            public PieChartToolTip Tip { get; set; }
            public Models.StatisticsAuthor Author { get; set; }
            public Point LabelAlignPoint { get; set; }
            public bool IsLeftSide => (StartAngle + EndAngle) > Math.PI;

            public Arc(double startAngle, double sweepAngle, string title, int count, double percentage, IBrush brush, Models.StatisticsAuthor author, Point labelAlignPoint)
            {
                StartAngle = startAngle;
                EndAngle = startAngle + sweepAngle;
                Tip = new PieChartToolTip(title, count, percentage, brush);
                Author = author;
                LabelAlignPoint = labelAlignPoint;
            }
        }

        private class LabelRender
        {
            public class Label
            {
                public FormattedText Author { get; set; }
                public FormattedText Percent { get; set; }
                public IBrush Color { get; set; }
                public Point AlignPoint { get; set; }
                public double Width { get; set; }
            }

            public class Batch
            {
                public Batch(Label info, double bottomY)
                {
                    _labels.Add(info);
                    _bottomY = bottomY;
                }

                public bool TryAppend(Label info, double desiredTop)
                {
                    if (desiredTop > _bottomY)
                        return false;

                    _labels.Add(info);

                    var midY = (_labels[0].AlignPoint.Y + info.AlignPoint.Y) * 0.5;
                    var height = Math.Ceiling(_labels.Count / 2.0) * LABEL_HEIGHT;
                    _bottomY = Math.Max(midY + height * 0.5, _labels[0].AlignPoint.Y + height - LABEL_HEIGHT * 0.5);
                    return true;
                }

                public void Draw(DrawingContext context, bool isLeftSide)
                {
                    if (_labels.Count == 0)
                        return;

                    if (_labels.Count == 1)
                    {
                        var label = _labels[0];
                        var x = isLeftSide ? label.AlignPoint.X - 8 - label.Width : label.AlignPoint.X + 8;
                        var y = label.AlignPoint.Y;
                        context.DrawText(label.Author, new Point(x, y - label.Author.Height * 0.5));
                        x += label.Author.WidthIncludingTrailingWhitespace + 4;
                        context.DrawText(label.Percent, new Point(x, y - label.Percent.Height * 0.5));
                        x += label.Percent.WidthIncludingTrailingWhitespace + 4;
                        context.DrawEllipse(label.Color, null, new Point(x + 4, y), 4, 4);
                    }
                    else
                    {
                        var first = _labels[0];
                        var last = _labels[^1];
                        var midY = (first.AlignPoint.Y + last.AlignPoint.Y) * 0.5;
                        var requiredHeight = Math.Ceiling(_labels.Count / 2.0) * LABEL_HEIGHT;
                        var alignedX = first.AlignPoint.X;
                        var startY = Math.Max(midY - requiredHeight * 0.5, first.AlignPoint.Y - LABEL_HEIGHT * 0.5);
                        var startX = 0.0;

                        for (var i = 0; i < _labels.Count; i++)
                        {
                            var info = _labels[i];
                            if (i % 2 == 0)
                            {
                                if (isLeftSide)
                                {
                                    startX = alignedX - 8 - info.Width;
                                    if (i + 1 < _labels.Count)
                                        startX = startX - 8 - _labels[i + 1].Width;
                                }
                                else
                                {
                                    startX = alignedX + 8;
                                }

                                if (i > 0)
                                    startY += LABEL_HEIGHT;
                            }

                            context.DrawText(info.Author, new Point(startX, startY));
                            startX += info.Author.WidthIncludingTrailingWhitespace + 4;
                            context.DrawText(info.Percent, new Point(startX, startY));
                            startX += info.Percent.WidthIncludingTrailingWhitespace + 4;
                            context.DrawEllipse(info.Color, null, new Point(startX + 4, startY + LABEL_HEIGHT * 0.5), 4, 4);
                            startX += 16;
                        }
                    }
                }

                private List<Label> _labels = new List<Label>();
                private double _bottomY = 0;
            }

            public void Append(Arc arc, Typeface typeface, Typeface secondaryTypeface, IBrush foreground)
            {
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
                    Brushes.Gray);

                var width = title.WidthIncludingTrailingWhitespace + percentage.WidthIncludingTrailingWhitespace + 16;
                var info = new Label()
                {
                    Author = title,
                    Percent = percentage,
                    Color = arc.Tip.Brush,
                    AlignPoint = arc.LabelAlignPoint,
                    Width = width,
                };

                if (arc.IsLeftSide)
                    _left.Add(info);
                else
                    _right.Add(info);
            }

            public void Draw(DrawingContext context)
            {
                var batches = new List<Batch>();

                for (var i = _left.Count - 1; i >= 0; i--)
                {
                    var info = _left[i];
                    var desiredTop = info.AlignPoint.Y - 8;
                    var desiredBottom = info.AlignPoint.Y + 8;
                    if (batches.Count == 0 || !batches[^1].TryAppend(info, desiredTop))
                    {
                        var batch = new Batch(info, desiredBottom);
                        batches.Add(batch);
                    }
                }

                foreach (var b in batches)
                    b.Draw(context, true);

                batches.Clear();
                foreach (var info in _right)
                {
                    var desiredTop = info.AlignPoint.Y - 8;
                    var desiredBottom = info.AlignPoint.Y + 8;
                    if (batches.Count == 0 || !batches[^1].TryAppend(info, desiredTop))
                    {
                        var batch = new Batch(info, desiredBottom);
                        batches.Add(batch);
                    }
                }

                foreach (var b in batches)
                    b.Draw(context, false);
            }

            private List<Label> _left = new List<Label>();
            private List<Label> _right = new List<Label>();
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

        private const double LABEL_HEIGHT = 14;

        private Models.StatisticsReport _report = null;
        private Point _center = new Point(0, 0);
        private double _radius = 0;
        private List<Arc> _arcs = new List<Arc>();
        private Arc _lastHovered = null;
        private PieChartToolTip _hoveringTip = null;
    }
}
