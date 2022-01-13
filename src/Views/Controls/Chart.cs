using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SourceGit.Views.Controls {
    /// <summary>
    ///     绘制提交频率柱状图
    /// </summary>
    public class Chart : FrameworkElement {
        public static readonly int LABEL_UNIT = 32;
        public static readonly double MAX_SHAPE_WIDTH = 24;

        public static readonly DependencyProperty LineBrushProperty = DependencyProperty.Register(
            "LineBrush", 
            typeof(Brush), 
            typeof(Chart), 
            new PropertyMetadata(Brushes.White));

        public Brush LineBrush {
            get { return (Brush)GetValue(LineBrushProperty); }
            set { SetValue(LineBrushProperty, value); }
        }

        public static readonly DependencyProperty ChartBrushProperty = DependencyProperty.Register(
            "ChartBrush",
            typeof(Brush),
            typeof(Chart),
            new PropertyMetadata(Brushes.White));

        public Brush ChartBrush {
            get { return (Brush)GetValue(ChartBrushProperty); }
            set { SetValue(ChartBrushProperty, value); }
        }

        private List<Models.StatisticSample> samples = new List<Models.StatisticSample>();
        private List<Rect> hitboxes = new List<Rect>();
        private int maxV = 0;

        /// <summary>
        ///     设置绘制数据
        /// </summary>
        /// <param name="samples">数据源</param>
        public void SetData(List<Models.StatisticSample> samples) {
            this.samples = samples;
            this.hitboxes.Clear();

            maxV = 0;
            foreach (var s in samples) {
                if (maxV < s.Count) maxV = s.Count;
            }
            maxV = (int)Math.Ceiling(maxV / 10.0) * 10;
            
            InvalidateVisual();
        }

        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e);
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc) {
            base.OnRender(dc);

            var font = new FontFamily("Consolas");
            var pen = new Pen(LineBrush, 1);
            dc.DrawLine(pen, new Point(LABEL_UNIT, 0), new Point(LABEL_UNIT, ActualHeight - LABEL_UNIT));
            dc.DrawLine(pen, new Point(LABEL_UNIT, ActualHeight - LABEL_UNIT), new Point(ActualWidth, ActualHeight - LABEL_UNIT));

            if (samples.Count == 0) return;

            var stepV = (ActualHeight - LABEL_UNIT) / 5;
            var labelStepV = maxV / 5;
            var gridPen = new Pen(LineBrush, 1) { DashStyle = DashStyles.Dash };
            for (int i = 1; i < 5; i++) {
                var vLabel = new FormattedText(
                    $"{maxV - i * labelStepV}",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(font, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                    12.0,
                    LineBrush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                var dashHeight = i * stepV;
                var vy = Math.Max(0, dashHeight - vLabel.Height * 0.5);
                dc.PushOpacity(.1);
                dc.DrawLine(gridPen, new Point(LABEL_UNIT + 1, dashHeight), new Point(ActualWidth, dashHeight));
                dc.Pop();
                dc.DrawText(vLabel, new Point(0, vy));
            }

            var stepX = (ActualWidth - LABEL_UNIT) / samples.Count;
            if (hitboxes.Count == 0) {
                var shapeWidth = Math.Min(LABEL_UNIT, stepX - 4);
                for (int i = 0; i < samples.Count; i++) {
                    var h = samples[i].Count * (ActualHeight - LABEL_UNIT) / maxV;
                    var x = LABEL_UNIT + 1 + stepX * i + (stepX - shapeWidth) * 0.5;
                    var y = ActualHeight - LABEL_UNIT - h;
                    hitboxes.Add(new Rect(x, y, shapeWidth, h));
                }
            }

            var mouse = Mouse.GetPosition(this);
            for (int i = 0; i < samples.Count; i++) {
                var hLabel = new FormattedText(
                    samples[i].Name,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(font, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                    10.0,
                    LineBrush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
                var rect = hitboxes[i];
                var xLabel = rect.X - (hLabel.Width - rect.Width) * 0.5;
                var yLabel = ActualHeight - LABEL_UNIT + 4;

                dc.DrawRectangle(ChartBrush, null, rect);

                if (stepX < LABEL_UNIT) {
                    dc.PushTransform(new TranslateTransform(xLabel, yLabel));
                    dc.PushTransform(new RotateTransform(45, hLabel.Width * 0.5, hLabel.Height * 0.5));
                    dc.DrawText(hLabel, new Point(0, 0));
                    dc.Pop();
                    dc.Pop();
                } else {
                    dc.DrawText(hLabel, new Point(xLabel, yLabel));
                }           
            }

            for (int i = 0; i < samples.Count; i++) {
                var rect = hitboxes[i];
                if (rect.Contains(mouse)) {
                    var tooltip = new FormattedText(
                        $"{samples[i].Count}",
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(font, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                        12.0,
                        FindResource("Brush.FG1") as Brush,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip);

                    var tx = rect.X - (tooltip.Width - rect.Width) * 0.5;
                    var ty = rect.Y - tooltip.Height - 4;
                    dc.DrawText(tooltip, new Point(tx, ty));
                    break;
                }
            }
        }
    }
}
