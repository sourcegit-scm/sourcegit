using System;
using System.Collections.Generic;
using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace SourceGit.Views
{
    public class ChartToolTip
    {
        public string Title { get; set; } = string.Empty;
        public bool HasUser { get; set; } = false;
        public int Total { get; set; } = 0;
        public int User { get; set; } = 0;

        public ChartToolTip(string title, bool hasUser)
        {
            Title = title;
            HasUser = hasUser;
        }
    }

    public class Chart : Control
    {
        public static readonly StyledProperty<Models.StatisticsSeries> SeriesProperty =
            AvaloniaProperty.Register<Chart, Models.StatisticsSeries>(nameof(Series));

        public Models.StatisticsSeries Series
        {
            get => GetValue(SeriesProperty);
            set => SetValue(SeriesProperty, value);
        }

        public static readonly StyledProperty<Models.StatisticsMode> ModeProperty =
            AvaloniaProperty.Register<Chart, Models.StatisticsMode>(nameof(Mode));

        public Models.StatisticsMode Mode
        {
            get => GetValue(ModeProperty);
            set => SetValue(ModeProperty, value);
        }

        public static readonly StyledProperty<IBrush> SampleBrushProperty =
            AvaloniaProperty.Register<Chart, IBrush>(nameof(SampleBrush), Brushes.SkyBlue);

        public IBrush SampleBrush
        {
            get => GetValue(SampleBrushProperty);
            set => SetValue(SampleBrushProperty, value);
        }

        public Chart()
        {
            ToolTip.SetPlacement(this, PlacementMode.Pointer);
            ToolTip.SetShowDelay(this, 0);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var series = Series;
            if (series == null || series.TotalSamples == null || series.MaxSampleValue == 0)
                return;

            var w = Bounds.Width;
            var h = Bounds.Height;
            context.FillRectangle(Brushes.Transparent, new Rect(0, 0, w, h));

            var mode = Mode;
            var hasUserSamples = series.UserSamples != null;
            var count = mode switch
            {
                Models.StatisticsMode.All => (series.MaxSampleTime.Year - series.MinSampleTime.Year) * 12 + (series.MaxSampleTime.Month - series.MinSampleTime.Month) + 1,
                _ => series.TotalSamples.Count,
            };

            var maxValue = GetTheMaxValueInChart(series.MaxSampleValue);
            var labelPen = new Pen(new SolidColorBrush(Colors.Gray, 0.4), .5);
            var leftMargin = 0.0;
            for (var i = 1; i <= 8; i++)
            {
                var percent = i * 0.125;
                var value = Math.Floor(maxValue * (1.0 - percent));
                var y = Math.Floor((h - 24) * percent) + 0.5;

                var label = new FormattedText(
                    $"{value}",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    11,
                    Brushes.Gray);

                if (leftMargin == 0)
                    leftMargin = label.Width + 24.0;

                context.DrawText(label, new Point(leftMargin - label.Width - 8.0, y - label.Height * 0.5));
                context.DrawLine(labelPen, new Point(leftMargin, y), new Point(w, y));
            }

            var step = Math.Max((w - leftMargin) / count, 10.0);
            _maxOffsetX = Math.Max(count * step - (w - leftMargin), 0);
            _hitBoxes.Clear();
            _lastHitted = new Rect(0, 0, 0, 0);

            var x = w + Math.Min(_maxOffsetX, _offsetX);
            var corner = new CornerRadius(2, 2, 0, 0);
            var sampleBrush = SampleBrush;
            var time = series.MaxSampleTime;
            var sampleW = hasUserSamples ? Math.Min((step - 4) * 0.5, 14.0) : Math.Min(step - 4, 28.0);
            var maxSampleH = h - 24.0;
            var maxLabelEndX = w - 4.0;

            using var clip = context.PushClip(new Rect(leftMargin, 0, w - leftMargin, h));
            do
            {
                var label = mode switch
                {
                    Models.StatisticsMode.All => time.ToString("yyyy/MM"),
                    Models.StatisticsMode.ThisMonth => time.ToString("MM/dd"),
                    _ => WEEKDAYS[(int)time.DayOfWeek]
                };

                if (x - step <= w && series.TotalSamples.TryGetValue(time, out var total) && total > 0)
                {
                    if (hasUserSamples)
                    {
                        var startX = x - step * 0.5 - 1 - sampleW;
                        var startY = maxSampleH * (1.0 - total * 1.0 / maxValue);
                        var rect = new Rect(startX, startY, sampleW, maxSampleH - startY);
                        var tip = new ChartToolTip(label, true);
                        tip.Total = total;

                        using (var opacity = context.PushOpacity(0.2))
                            context.DrawRectangle(sampleBrush, null, new RoundedRect(rect, corner));

                        if (series.UserSamples.TryGetValue(time, out var user) && user > 0)
                        {
                            var userStartX = startX + sampleW + 2;
                            var userStartY = maxSampleH * (1.0 - user * 1.0 / maxValue);
                            var userRect = new Rect(userStartX, userStartY, sampleW, maxSampleH - userStartY);
                            context.DrawRectangle(sampleBrush, null, new RoundedRect(userRect, corner));
                            tip.User = user;
                        }

                        var hitRect = new Rect(startX, startY, sampleW * 2 + 2, maxSampleH - startY);
                        _hitBoxes.Add(new(hitRect, tip));
                    }
                    else
                    {
                        var startX = x - step * 0.5 - sampleW * 0.5;
                        var startY = maxSampleH * (1.0 - total * 1.0 / maxValue);
                        var rect = new Rect(startX, startY, sampleW, maxSampleH - startY);

                        context.DrawRectangle(sampleBrush, null, new RoundedRect(rect, corner));

                        var tip = new ChartToolTip(label, false) { Total = total };
                        _hitBoxes.Add(new(rect, tip));
                    }
                }

                if (x <= w)
                {
                    var formattedLabel = new FormattedText(
                        label,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        Typeface.Default,
                        11,
                        Brushes.Gray);

                    var labelCenterX = x - step * 0.5;
                    var labelEndX = labelCenterX + formattedLabel.Width * 0.5;
                    if (labelEndX <= maxLabelEndX)
                    {
                        var labelStartX = labelCenterX - formattedLabel.Width * 0.5;
                        var labelStartY = h - formattedLabel.Height - 2.0;
                        context.DrawText(formattedLabel, new Point(labelStartX, labelStartY));
                        maxLabelEndX = labelStartX - 16.0;
                    }
                }

                if (x <= leftMargin)
                    break;

                x -= step;
                time = mode switch
                {
                    Models.StatisticsMode.All => time.Month == 1 ? new DateTime(time.Year - 1, 12, 1).ToLocalTime().Date : new DateTime(time.Year, time.Month - 1, 1).ToLocalTime().Date,
                    _ => time.AddDays(-1),
                };

                if (time < series.MinSampleTime)
                    break;
            } while (true);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SeriesProperty || change.Property == ModeProperty)
            {
                _offsetX = 0;
                InvalidateVisual();
            }
            else if (change.Property == SampleBrushProperty)
            {
                InvalidateVisual();
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _isDraging = true;
                _lastDragX = e.GetPosition(this).X;
                e.Pointer.Capture(this);
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            if (_isDraging)
            {
                var posX = e.GetPosition(this).X;
                if (Math.Abs(posX - _lastDragX) < 0.5f)
                    return;

                var desired = Math.Max(0, Math.Min(_offsetX + posX - _lastDragX, _maxOffsetX));
                if (Math.Abs(desired - _offsetX) < 0.5f)
                    return;

                _offsetX = desired;
                _lastDragX = posX;
                _lastHitted = new Rect(0, 0, 0, 0);
                InvalidateVisual();
            }
            else
            {
                var p = e.GetPosition(this);

                if (!_lastHitted.Contains(p))
                {
                    foreach (var box in _hitBoxes)
                    {
                        if (box.Rect.Contains(p))
                        {
                            ToolTip.SetTip(this, box.ToolTip);
                            _lastHitted = box.Rect;
                            return;
                        }
                    }

                    _lastHitted = new Rect(0, 0, 0, 0);
                    ToolTip.SetTip(this, null);
                }
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            _isDraging = false;
            e.Pointer.Capture(null);
        }

        private double GetTheMaxValueInChart(int maxSampleValue)
        {
            if (maxSampleValue < 8)
                return 8;

            if (maxSampleValue < 16)
                return 16;

            return Math.Floor(maxSampleValue / 6.0) * 8.0;
        }

        private record HitBox(Rect Rect, ChartToolTip ToolTip);

        private static readonly string[] WEEKDAYS = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN"];
        private double _offsetX = 0;
        private double _maxOffsetX = 0;
        private bool _isDraging = false;
        private double _lastDragX = 0;
        private List<HitBox> _hitBoxes = [];
        private Rect _lastHitted = new(0, 0, 0, 0);
    }
}
