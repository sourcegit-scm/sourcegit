using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SourceGit.UI {

    /// <summary>
    ///     Common popup manager.
    /// </summary>
    public partial class PopupManager : UserControl {
        private bool locked = false;

        /// <summary>
        ///     Constructor.
        /// </summary>
        public PopupManager() {
            InitializeComponent();
        }

        /// <summary>
        ///     Show content as popup.
        /// </summary>
        /// <param name="elem"></param>
        public void Show(UIElement elem) {
            if (locked) return;

            var gone = new Thickness(0, -(double)elem.GetValue(HeightProperty) - 16, 0, 0);

            ThicknessAnimation anim = new ThicknessAnimation();
            anim.Duration = TimeSpan.FromMilliseconds(150);
            anim.From = gone;
            anim.To = new Thickness(0);

            statusMsg.Content = "";
            popupContent.Child = elem;
            popupContent.Margin = gone;
            Visibility = Visibility.Visible;
            popupContent.BeginAnimation(MarginProperty, anim);
        }

        /// <summary>
        ///     Is current locked.
        /// </summary>
        /// <returns></returns>
        public bool IsLocked() {
            return locked;
        }

        /// <summary>
        ///     Lock
        /// </summary>
        public void Lock() {
            locked = true;
            status.Visibility = Visibility.Visible;

            DoubleAnimation anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1));
            anim.RepeatBehavior = RepeatBehavior.Forever;
            statusIcon.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, anim);
        }

        /// <summary>
        ///     Unlock
        /// </summary>
        public void Unlock() {
            locked = false;
            statusIcon.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, null);
            status.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        ///     Update status description
        /// </summary>
        /// <param name="desc"></param>
        public void UpdateStatus(string desc) {
            Dispatcher.Invoke(() => {
                statusMsg.Content = desc;
            });
        }

        /// <summary>
        ///     Close current popup.
        /// </summary>
        /// <param name="unlockFirst"></param>
        public void Close(bool unlockFirst = false) {
            if (popupContent.Child == null) return;
            if (locked && !unlockFirst) return;
            locked = false;

            ThicknessAnimation anim = new ThicknessAnimation();
            anim.Duration = TimeSpan.FromMilliseconds(150);
            anim.From = new Thickness(0);
            anim.To = new Thickness(0, -(double)popupContent.Child.GetValue(HeightProperty) - 16, 0, 0);
            anim.Completed += (obj, ev) => {
                Visibility = Visibility.Collapsed;
                popupContent.Child = null;
            };
            
            popupContent.BeginAnimation(MarginProperty, anim);            
            statusIcon.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, null);
            status.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        ///     Close by click blank area. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Close(object sender, RoutedEventArgs e) {
            Close();
        }
    }
}
