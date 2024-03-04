using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SourceGit.Views {
    public class Chart : Control {
        public static readonly StyledProperty<FontFamily> FontFamilyProperty =
            AvaloniaProperty.Register<Chart, FontFamily>(nameof(FontFamily));

        public FontFamily FontFamily {
            get => GetValue(FontFamilyProperty);
            set => SetValue(FontFamilyProperty, value);
        }

        public static readonly StyledProperty<IBrush> LineBrushProperty =
            AvaloniaProperty.Register<Chart, IBrush>(nameof(LineBrush), Brushes.Gray);

        public IBrush LineBrush {
            get => GetValue(LineBrushProperty);
            set => SetValue(LineBrushProperty, value);
        }

        public static readonly StyledProperty<IBrush> ShapeBrushProperty =
            AvaloniaProperty.Register<Chart, IBrush>(nameof(ShapeBrush), Brushes.Gray);

        public IBrush ShapeBrush {
            get => GetValue(ShapeBrushProperty);
            set => SetValue(ShapeBrushProperty, value);
        }

        public static readonly StyledProperty<List<Models.StatisticsSample>> SamplesProperty =
            AvaloniaProperty.Register<Chart, List<Models.StatisticsSample>>(nameof(Samples), null);

        public List<Models.StatisticsSample> Samples {
            get => GetValue(SamplesProperty);
            set => SetValue(SamplesProperty, value);
        }

        static Chart() {
            AffectsRender<Chart>(SamplesProperty);
        }

        public override void Render(DrawingContext context) {
            if (Samples == null) return;

            var samples = Samples;
            int maxV = 0;
            foreach (var s in samples) {
                if (maxV < s.Count) maxV = s.Count;
            }

            if (maxV < 5) {
                maxV = 5;
            } else if (maxV < 10) {
                maxV = 10;
            } else if (maxV < 50) {
                maxV = 50;
            } else if (maxV < 100) {
                maxV = 100;
            } else if (maxV < 200) {
                maxV = 200;
            } else if (maxV < 500) {
                maxV = 500;
            } else {
                maxV = (int)Math.Ceiling(maxV / 500.0) * 500;
            }

            var typeface = new Typeface(FontFamily);
            var pen = new Pen(LineBrush, 1);
            var width = Bounds.Width;
            var height = Bounds.Height;

            // Draw coordinate
            var maxLabel = new FormattedText($"{maxV}", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 12.0, LineBrush);
            var horizonStart = maxLabel.Width + 8;
            var labelHeight = maxLabel.Height;
            context.DrawText(maxLabel, new Point(0, -maxLabel.Height * 0.5));
            context.DrawLine(pen, new Point(horizonStart, 0), new Point(horizonStart, height - labelHeight));
            context.DrawLine(pen, new Point(horizonStart, height - labelHeight), new Point(width, height - labelHeight));

            if (samples.Count == 0) return;

            // Draw horizontal lines
            var stepX = (width - horizonStart) / samples.Count;
            var stepV = (height - labelHeight) / 5;
            var labelStepV = maxV / 5;
            var gridPen = new Pen(LineBrush, 1, new DashStyle());
            for (int i = 1; i < 5; i++) {
                var vLabel = new FormattedText(
                    $"{maxV - i * labelStepV}",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    12.0,
                    LineBrush);

                var dashHeight = i * stepV;
                var vy = Math.Max(0, dashHeight - vLabel.Height * 0.5);
                using (context.PushOpacity(.1)) {
                    context.DrawLine(gridPen, new Point(horizonStart + 1, dashHeight), new Point(width, dashHeight));
                }
                context.DrawText(vLabel, new Point(horizonStart - vLabel.Width - 8, vy));
            }

            // Calculate hit boxes
            var shapeWidth = Math.Min(32, stepX - 4);
            var hitboxes = new List<Rect>();
            for (int i = 0; i < samples.Count; i++) {
                var h = samples[i].Count * (height - labelHeight) / maxV;
                var x = horizonStart + 1 + stepX * i + (stepX - shapeWidth) * 0.5;
                var y = height - labelHeight - h;
                hitboxes.Add(new Rect(x, y, shapeWidth, h));
            }

            // Draw shapes
            for (int i = 0; i < samples.Count; i++) {
                var hLabel = new FormattedText(
                    samples[i].Name,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    10.0,
                    LineBrush);
                var rect = hitboxes[i];
                var xLabel = rect.X - (hLabel.Width - rect.Width) * 0.5;
                var yLabel = height - labelHeight + 4;

                context.DrawRectangle(ShapeBrush, null, rect);

                if (stepX < 32) {
                    var matrix = Matrix.CreateTranslation(hLabel.Width * 0.5, -hLabel.Height * 0.5) // Center of label
                        * Matrix.CreateRotation(Math.PI * 0.25) // Rotate
                        * Matrix.CreateTranslation(xLabel, yLabel); // Move
                    using (context.PushTransform(matrix)) {
                        context.DrawText(hLabel, new Point(0, 0));
                    }
                } else {
                    context.DrawText(hLabel, new Point(xLabel, yLabel));
                }
            }

            // Draw labels on hover
            for (int i = 0; i < samples.Count; i++) {
                var rect = hitboxes[i];
                if (rect.Contains(_mousePos)) {
                    var tooltip = new FormattedText(
                        $"{samples[i].Count}",
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        12.0,
                        LineBrush);

                    var tx = rect.X - (tooltip.Width - rect.Width) * 0.5;
                    var ty = rect.Y - tooltip.Height - 4;
                    context.DrawText(tooltip, new Point(tx, ty));
                    break;
                }
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e) {
            base.OnPointerMoved(e);
            _mousePos = e.GetPosition(this);
            InvalidateVisual();
        }

        private Point _mousePos = new Point(0, 0);
    }

    public partial class Statistics : Window {
        public Statistics() {
            InitializeComponent();
        }

        private void CloseWindow(object sender, RoutedEventArgs e) {
            Close();
        }
    }
}
