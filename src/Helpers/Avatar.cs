using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SourceGit.Helpers {

    /// <summary>
    ///     Avatar control
    /// </summary>
    public class Avatar : Image {

        /// <summary>
        ///     Colors used in avatar
        /// </summary>
        public static Brush[] Colors = new Brush[] {
            Brushes.DarkBlue,
            Brushes.DarkCyan,
            Brushes.DarkGoldenrod,
            Brushes.DarkGray,
            Brushes.DarkGreen,
            Brushes.DarkKhaki,
            Brushes.DarkMagenta,
            Brushes.DarkOliveGreen,
            Brushes.DarkOrange,
            Brushes.DarkOrchid,
            Brushes.DarkRed,
            Brushes.DarkSalmon,
            Brushes.DarkSeaGreen,
            Brushes.DarkSlateBlue,
            Brushes.DarkSlateGray,
            Brushes.DarkTurquoise,
            Brushes.DarkViolet
        };

        /// <summary>
        ///     User property definition.
        /// </summary>
        public static readonly DependencyProperty UserProperty = DependencyProperty.Register(
            "User", 
            typeof(Git.User), 
            typeof(Avatar), 
            new PropertyMetadata(null, OnUserChanged));

        /// <summary>
        ///     User property
        /// </summary>
        public Git.User User {
            get { return (Git.User)GetValue(UserProperty); }
            set { SetValue(UserProperty, value); }
        }

        /// <summary>
        ///     Loading request
        /// </summary>
        private class Request {
            public BitmapImage img = null;
            public List<Avatar> targets = new List<Avatar>();
        }

        /// <summary>
        ///     Path to cache downloaded avatars
        /// </summary>
        private static readonly string CACHE_PATH = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SourceGit",
            "avatars");

        /// <summary>
        ///     Current requests.
        /// </summary>
        private static Dictionary<string, Request> requesting = new Dictionary<string, Request>();

        /// <summary>
        ///     Render implementation.
        /// </summary>
        /// <param name="dc"></param>
        protected override void OnRender(DrawingContext dc) {
            base.OnRender(dc);

            if (Source == null && User != null) {
                var placeholder = User.Name.Length > 0 ? User.Name.Substring(0, 1) : "?";
                var formatted = new FormattedText(
                    placeholder,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                    Width * 0.75,
                    Brushes.White,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                double offsetX = 0;
                if (HorizontalAlignment == HorizontalAlignment.Right) {
                    offsetX = -Width * 0.5;
                }

                var chars = placeholder.ToCharArray();
                var sum = 0;
                foreach (var ch in chars) sum += Math.Abs(ch);
                var brush = Colors[sum % Colors.Length];

                dc.DrawRoundedRectangle(brush, null, new Rect(-Width * 0.5 + offsetX, -Height * 0.5, Width, Height), Width / 16, Height / 16);
                dc.DrawText(formatted, new Point(formatted.Width * -0.5 + offsetX, formatted.Height * -0.5));
            }
        }

        /// <summary>
        ///     Reset image.
        /// </summary>
        private void ReloadImage(Git.User oldUser) {
            if (oldUser != null && requesting.ContainsKey(oldUser.Email)) {
                if (requesting[oldUser.Email].targets.Count <= 1) {
                    requesting.Remove(oldUser.Email);
                } else {
                    requesting[oldUser.Email].targets.Remove(this);
                }
            }

            Source = null;
            InvalidateVisual();

            if (User == null) return;

            var email = User.Email;
            if (requesting.ContainsKey(email)) {
                requesting[email].targets.Add(this);
                return;
            }

            byte[] hash = MD5.Create().ComputeHash(Encoding.Default.GetBytes(email.ToLower().Trim()));
            string md5 = "";
            for (int i = 0; i < hash.Length; i++) md5 += hash[i].ToString("x2");
            md5 = md5.ToLower();

            string filePath = Path.Combine(CACHE_PATH, md5);
            if (File.Exists(filePath)) {
                Source = new BitmapImage(new Uri(filePath));
                return;
            }

            requesting.Add(email, new Request());
            requesting[email].targets.Add(this);

            BitmapImage downloading = new BitmapImage(new Uri("https://www.gravatar.com/avatar/" + md5 + "?d=404"));
            requesting[email].img = downloading;
            downloading.DownloadCompleted += (o, e) => {
                var owner = o as BitmapImage;
                if (owner != null) {
                    if (!Directory.Exists(CACHE_PATH)) Directory.CreateDirectory(CACHE_PATH);

                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(owner));
                    using (var fs = new FileStream(filePath, FileMode.Create)) {
                        encoder.Save(fs);
                    }

                    if (requesting.ContainsKey(email)) {
                        BitmapImage exists = new BitmapImage(new Uri(filePath));
                        foreach (var one in requesting[email].targets) one.Source = exists;
                        requesting.Remove(email);
                    }
                }
            };
            downloading.DownloadFailed += (o, e) => {
                requesting.Remove(email);
            };
        }

        /// <summary>
        ///     Callback on user property changed
        /// </summary>
        /// <param name="d"></param>
        /// <param name="e"></param>
        private static void OnUserChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            Avatar a = d as Avatar;
            if (a != null) a.ReloadImage(e.OldValue as Git.User);
        }
    }
}
