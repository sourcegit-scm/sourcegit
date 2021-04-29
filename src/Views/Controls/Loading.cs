using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace SourceGit.Views.Controls {

    /// <summary>
    ///     加载中图标
    /// </summary>
    public class Loading : UserControl {
        private Path icon = null;

        public static readonly DependencyProperty IsAnimatingProperty = DependencyProperty.Register(
            "IsAnimating", 
            typeof(bool), 
            typeof(Loading), 
            new PropertyMetadata(false, OnIsAnimatingChanged));

        public bool IsAnimating {
            get { return (bool)GetValue(IsAnimatingProperty); }
            set { SetValue(IsAnimatingProperty, value); }
        }

        public Loading() {
            icon = new Path();
            icon.Data = FindResource("Icon.Loading") as Geometry;
            icon.RenderTransformOrigin = new Point(.5, .5);
            icon.RenderTransform = new RotateTransform(0);
            icon.Width = double.NaN;
            icon.Height = double.NaN;

            AddChild(icon);
        }

        private static void OnIsAnimatingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var loading = d as Loading;
            if (loading == null) return;

            if (loading.IsAnimating) {
                DoubleAnimation anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1));
                anim.RepeatBehavior = RepeatBehavior.Forever;
                loading.icon.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, anim);
            } else {
                loading.icon.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, null);
            }
        }
    }
}
