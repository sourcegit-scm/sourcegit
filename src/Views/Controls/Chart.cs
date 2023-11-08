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

            if (maxV <= 5) {
                maxV = 5;
            } else if (maxV <= 10) {
                maxV = 10;
            } else if (maxV <= 50) {
                maxV = 50;
            } else if (maxV <= 100) {
                maxV = 100;
            } else if (maxV <= 200) {
                maxV = 200;
            } else if (maxV <= 500) {
                maxV = 500;
            } else {
                maxV = (int)Math.Ceiling(maxV / 500.0) * 500;
            }            
            
            InvalidateVisual();
        }

        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e);
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc) {
            base.OnRender(dc);

            var font = new FontFamily("Consolas");
            var culture = CultureInfo.CurrentCulture;
            var typeface = new Typeface(font, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var ppi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var pen = new Pen(LineBrush, 1);
            
            // 坐标系绘制
            var maxLabel = new FormattedText($"{maxV}", culture, FlowDirection.LeftToRight, typeface, 12.0, LineBrush, ppi);
            var horizonStart = maxLabel.Width + 8;
            var labelHeight = 32;
            dc.DrawText(maxLabel, new Point(0, - maxLabel.Height * 0.5));
            dc.DrawLine(pen, new Point(horizonStart, 0), new Point(horizonStart, ActualHeight - labelHeight));
            dc.DrawLine(pen, new Point(horizonStart, ActualHeight - labelHeight), new Point(ActualWidth, ActualHeight - labelHeight));

            if (samples.Count == 0) return;

            // 绘制纵坐标数值参考线
            var stepX = (ActualWidth - horizonStart) / samples.Count;
            var stepV = (ActualHeight - labelHeight) / 5;
            var labelStepV = maxV / 5;
            var gridPen = new Pen(LineBrush, 1) { DashStyle = DashStyles.Dash };
            for (int i = 1; i < 5; i++) {
                var vLabel = new FormattedText(
                    $"{maxV - i * labelStepV}",
                    culture,
                    FlowDirection.LeftToRight,
                    typeface,
                    12.0,
                    LineBrush,
                    ppi);

                var dashHeight = i * stepV;
                var vy = Math.Max(0, dashHeight - vLabel.Height * 0.5);
                dc.PushOpacity(.1);
                dc.DrawLine(gridPen, new Point(horizonStart + 1, dashHeight), new Point(ActualWidth, dashHeight));
                dc.Pop();
                dc.DrawText(vLabel, new Point(horizonStart - vLabel.Width - 8, vy));
            }

            // 先计算一下当前每个样本的碰撞区域，用于当鼠标移动上去时显示数值
            if (hitboxes.Count == 0) {
                var shapeWidth = Math.Min(32, stepX - 4);
                for (int i = 0; i < samples.Count; i++) {
                    var h = samples[i].Count * (ActualHeight - labelHeight) / maxV;
                    var x = horizonStart + 1 + stepX * i + (stepX - shapeWidth) * 0.5;
                    var y = ActualHeight - labelHeight - h;
                    hitboxes.Add(new Rect(x, y, shapeWidth, h));
                }
            }

            // 绘制样本
            for (int i = 0; i < samples.Count; i++) {
                var hLabel = new FormattedText(
                    samples[i].Name,
                    culture,
                    FlowDirection.LeftToRight,
                    typeface,
                    10.0,
                    LineBrush,
                    ppi);
                var rect = hitboxes[i];
                var xLabel = rect.X - (hLabel.Width - rect.Width) * 0.5;
                var yLabel = ActualHeight - labelHeight + 4;

                dc.DrawRectangle(ChartBrush, null, rect);

                if (stepX < 32) {
                    dc.PushTransform(new TranslateTransform(xLabel, yLabel));
                    dc.PushTransform(new RotateTransform(45, hLabel.Width * 0.5, hLabel.Height * 0.5));
                    dc.DrawText(hLabel, new Point(0, 0));
                    dc.Pop();
                    dc.Pop();
                } else {
                    dc.DrawText(hLabel, new Point(xLabel, yLabel));
                }           
            }

            // 当鼠标移动上去时显示数值
            var mouse = Mouse.GetPosition(this);
            for (int i = 0; i < samples.Count; i++) {
                var rect = hitboxes[i];
                if (rect.Contains(mouse)) {
                    var tooltip = new FormattedText(
                        $"{samples[i].Count}",
                        culture,
                        FlowDirection.LeftToRight,
                        typeface,
                        12.0,
                        FindResource("Brush.FG1") as Brush,
                        ppi);

                    var tx = rect.X - (tooltip.Width - rect.Width) * 0.5;
                    var ty = rect.Y - tooltip.Height - 4;
                    dc.DrawText(tooltip, new Point(tx, ty));
                    break;
                }
            }
        }
    }
}
