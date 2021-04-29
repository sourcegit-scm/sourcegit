using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace SourceGit.Views.Widgets {

    /// <summary>
    ///     统一的下拉弹出窗体面板
    /// </summary>
    public partial class PopupPanel : UserControl {
        private Controls.PopupWidget view = null;
        private bool locked = false;

        public bool IsLocked {
            get { return locked; }
        }

        public PopupPanel() {
            InitializeComponent();
        }

        public void Show(Controls.PopupWidget widget) {
            if (locked) return;
            view = widget;
            txtTitle.Text = widget.GetTitle();
            Visibility = Visibility.Hidden;
            container.Content = view;

            body.Margin = new Thickness(0, 0, 0, 0);
            body.UpdateLayout();

            var gone = new Thickness(0, -body.ActualHeight, 0, 0);
            body.Margin = gone;

            ThicknessAnimation anim = new ThicknessAnimation();
            anim.Duration = TimeSpan.FromMilliseconds(150);
            anim.From = gone;
            anim.To = new Thickness(0);
            Visibility = Visibility.Visible;
            body.BeginAnimation(MarginProperty, anim);
        }

        public void ShowAndStart(Controls.PopupWidget widget) {
            if (locked) return;
            Show(widget);
            Sure(null, null); 
        }

        public void UpdateProgress(string message) {
            Dispatcher.Invoke(() => txtMsg.Text = message);
        }

        public void Close() {
            if (Visibility != Visibility.Visible) return;

            ThicknessAnimation anim = new ThicknessAnimation();
            anim.Duration = TimeSpan.FromMilliseconds(150);
            anim.From = new Thickness(0);
            anim.To = new Thickness(0, -body.ActualHeight, 0, 0);
            anim.Completed += (obj, ev) => {
                Visibility = Visibility.Collapsed;
                container.Content = null;
                view = null;
                locked = false;
                mask.Visibility = Visibility.Collapsed;
                processing.IsAnimating = false;
                txtMsg.Text = "";
            };
            body.BeginAnimation(MarginProperty, anim);
        }

        private async void Sure(object sender, RoutedEventArgs e) {
            if (Visibility != Visibility.Visible) return;

            if (view == null) {
                Close();
                return;
            }

            if (locked) return;

            locked = true;
            mask.Visibility = Visibility.Visible;
            processing.IsAnimating = true;

            var task = view.Start();
            if (task != null) {
                var close = await task;
                if (close) {
                    Close();
                    return;
                }
            }

            locked = false;
            mask.Visibility = Visibility.Collapsed;
            processing.IsAnimating = false;
            txtMsg.Text = "";
        }

        private void Cancel(object sender, RoutedEventArgs e) {
            if (locked) return;
            Close();
        }
    }
}
