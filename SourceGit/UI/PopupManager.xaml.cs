using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace SourceGit.UI {

    /// <summary>
    ///     Common popup manager.
    /// </summary>
    public partial class PopupManager : UserControl {
        private static PopupManager instance = null;
        private static bool locked = false;

        /// <summary>
        ///     Constructor.
        /// </summary>
        public PopupManager() {
            instance = this;
            InitializeComponent();
        }

        /// <summary>
        ///     Show content as popup.
        /// </summary>
        /// <param name="elem"></param>
        public static void Show(UIElement elem) {
            if (instance == null || locked) return;

            var gone = new Thickness(0, -(double)elem.GetValue(HeightProperty) - 16, 0, 0);

            ThicknessAnimation anim = new ThicknessAnimation();
            anim.Duration = TimeSpan.FromMilliseconds(150);
            anim.From = gone;
            anim.To = new Thickness(0);

            instance.popupContent.Child = elem;
            instance.popupContent.Margin = gone;
            instance.Visibility = Visibility.Visible;
            instance.popupContent.BeginAnimation(MarginProperty, anim);
        }

        /// <summary>
        ///     Is current locked.
        /// </summary>
        /// <returns></returns>
        public static bool IsLocked() {
            return locked;
        }

        /// <summary>
        ///     Lock
        /// </summary>
        public static void Lock() {
            locked = true;
        }

        /// <summary>
        ///     Unlock
        /// </summary>
        public static void Unlock() {
            locked = false;
        }

        /// <summary>
        ///     Close current popup.
        /// </summary>
        /// <param name="unlockFirst"></param>
        public static void Close(bool unlockFirst = false) {
            if (instance == null) return;
            if (instance.popupContent.Child == null) return;
            if (locked && !unlockFirst) return;
            locked = false;

            ThicknessAnimation anim = new ThicknessAnimation();
            anim.Duration = TimeSpan.FromMilliseconds(150);
            anim.From = new Thickness(0);
            anim.To = new Thickness(0, -(double)instance.popupContent.Child.GetValue(HeightProperty) - 16, 0, 0);
            anim.Completed += (obj, ev) => {
                instance.Visibility = Visibility.Collapsed;
                instance.popupContent.Child = null;
            };
            instance.popupContent.BeginAnimation(MarginProperty, anim);
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
