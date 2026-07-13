using System;
using System.Collections.Generic;
using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace SourceGit.Views
{
    public record ChartToolTip(string Title, bool HasUser, int All, int User);

    public class Chart : Control
    {
        public static readonly DirectProperty<Chart, Models.StatisticsSamples> SamplesProperty =
            AvaloniaProperty.RegisterDirect<Chart, Models.StatisticsSamples>(
                nameof(Samples),
                static o => o.Samples,
                static (o, v) => o.Samples = v);

        public Models.StatisticsSamples Samples
        {
            get => _samples;
            set => SetAndRaise(SamplesProperty, ref _samples, value);
        }

        public static readonly StyledProperty<FontFamily> LabelFontFamilyProperty =
            AvaloniaProperty.Register<Chart, FontFamily>(nameof(LabelFontFamily));

        public FontFamily LabelFontFamily
        {
            get => GetValue(LabelFontFamilyProperty);
            set => SetValue(LabelFontFamilyProperty, value);
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

            var samples = _samples;
            if (samples == null || samples.Count == 0)
                return;

            var w = Bounds.Width;
            var h = Bounds.Height;
            context.FillRectangle(Brushes.Transparent, new Rect(0, 0, w, h));

            var count = samples.Count;
            var maxValue = samples.MaxValue;
            var time = samples.EndTime;
            var minTime = samples.StartTime;
            var hasUserSamples = samples.HasSpecialUser;

            var labelPen = new Pen(new SolidColorBrush(Colors.Gray, 0.4), .5);
            var labelTypeface = new Typeface(LabelFontFamily);
            var corner = new CornerRadius(2, 2, 0, 0);
            var sampleBrush = SampleBrush;

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
                    labelTypeface,
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
            var sampleW = hasUserSamples ? Math.Min(step * 0.5 - 2.5, 14.0) : Math.Min(step - 3, 28.0);
            var maxSampleH = h - 24.0;
            var maxLabelEndX = w - 4.0;

            using var clip = context.PushClip(new Rect(leftMargin, 0, w - leftMargin, h));
            do
            {
                var (label, total, user) = samples.GetSample(time);

                if (x - step <= w && total > 0)
                {
                    if (hasUserSamples)
                    {
                        var startX = x - step * 0.5 - 1 - sampleW;
                        var startY = maxSampleH * (1.0 - total * 1.0 / maxValue);
                        var rect = new Rect(startX, startY, sampleW, maxSampleH - startY);

                        using (context.PushOpacity(0.2))
                        {
                            context.DrawRectangle(sampleBrush, null, new RoundedRect(rect, corner));
                        }

                        if (user > 0)
                        {
                            var userStartX = startX + sampleW + 2;
                            var userStartY = maxSampleH * (1.0 - user * 1.0 / maxValue);
                            var userRect = new Rect(userStartX, userStartY, sampleW, maxSampleH - userStartY);
                            context.DrawRectangle(sampleBrush, null, new RoundedRect(userRect, corner));
                        }

                        var hitRect = new Rect(startX, startY, sampleW * 2 + 2, maxSampleH - startY);
                        _hitBoxes.Add(new(hitRect, new(label, true, total, user)));
                    }
                    else
                    {
                        var startX = x - step * 0.5 - sampleW * 0.5;
                        var startY = maxSampleH * (1.0 - total * 1.0 / maxValue);
                        var rect = new Rect(startX, startY, sampleW, maxSampleH - startY);

                        context.DrawRectangle(sampleBrush, null, new RoundedRect(rect, corner));
                        _hitBoxes.Add(new(rect, new(label, false, total, 0)));
                    }
                }

                if (x <= w)
                {
                    var formattedLabel = new FormattedText(
                        label,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        labelTypeface,
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

                time = samples.NextSampleTime(time);
                if (time < minTime)
                    break;
            } while (true);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SamplesProperty)
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

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            e.Handled = true;

            var deltaX = e.KeyModifiers == KeyModifiers.Shift ? e.Delta.Y : e.Delta.X;
            if (deltaX == 0)
                return;

            deltaX = Math.Min(32, Math.Max(-32, _maxOffsetX * deltaX));

            var desired = Math.Max(0, Math.Min(_offsetX + deltaX, _maxOffsetX));
            if (Math.Abs(desired - _offsetX) < 0.1)
                return;

            _offsetX = desired;
            _isDraging = false;
            _lastHitted = new Rect(0, 0, 0, 0);
            InvalidateVisual();
        }

        private record HitBox(Rect Rect, ChartToolTip ToolTip);

        private Models.StatisticsSamples _samples = null;
        private double _offsetX = 0;
        private double _maxOffsetX = 0;
        private bool _isDraging = false;
        private double _lastDragX = 0;
        private List<HitBox> _hitBoxes = [];
        private Rect _lastHitted = new(0, 0, 0, 0);
    }
}
