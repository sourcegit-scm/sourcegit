using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
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
        ///     Colors used in avatar for light theme
        /// </summary>
        public static Brush[] LightColors = new Brush[] {
            Brushes.LightCoral,
            Brushes.LightGreen,
            Brushes.LightPink,
            Brushes.LightSeaGreen,
            Brushes.LightSteelBlue,
            Brushes.Gray,
            Brushes.SkyBlue,
            Brushes.Plum,
            Brushes.Gold,
            Brushes.Khaki,
        };

        /// <summary>
        ///     Colors used in avatar for light theme
        /// </summary>
        public static Brush[] DarkColors = new Brush[] {
            Brushes.DarkCyan,
            Brushes.DarkGoldenrod,
            Brushes.DarkGreen,
            Brushes.DarkKhaki,
            Brushes.DarkMagenta,
            Brushes.DarkOliveGreen,
            Brushes.DarkOrange,
            Brushes.DarkOrchid,
            Brushes.DarkSalmon,
            Brushes.DarkSeaGreen,
            Brushes.DarkSlateBlue,
            Brushes.DarkSlateGray,
            Brushes.DarkTurquoise,
            Brushes.DarkViolet
        };

        /// <summary>
        ///     Path to cache downloaded avatars
        /// </summary>
        public static readonly string CACHE_PATH = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SourceGit",
            "avatars");

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
        ///     Current requests.
        /// </summary>
        private static Dictionary<string, List<Avatar>> requesting = new Dictionary<string, List<Avatar>>();

        /// <summary>
        ///     Loaded images.
        /// </summary>
        private static Dictionary<string, BitmapImage> loaded = new Dictionary<string, BitmapImage>();

        /// <summary>
        ///     Loader to join in queue.
        /// </summary>
        private static Task loader = null;

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

                Brush brush;
                if (App.Setting.UI.UseLightTheme) {
                    brush = LightColors[sum % LightColors.Length];
                } else {
                    brush = DarkColors[sum % DarkColors.Length];
                }

                dc.DrawRoundedRectangle(brush, null, new Rect(-Width * 0.5 + offsetX, -Height * 0.5, Width, Height), Width / 16, Height / 16);
                dc.DrawText(formatted, new Point(formatted.Width * -0.5 + offsetX, formatted.Height * -0.5));
            }
        }

        /// <summary>
        ///     Reset image.
        /// </summary>
        private void ReloadImage(Git.User oldUser) {
            if (oldUser != null && requesting.ContainsKey(oldUser.Email)) {
                if (requesting[oldUser.Email].Count <= 1) {
                    requesting.Remove(oldUser.Email);
                } else {
                    requesting[oldUser.Email].Remove(this);
                }
            }

            Source = null;
            InvalidateVisual();

            if (User == null) return;

            var email = User.Email;
            if (loaded.ContainsKey(email)) {
                Source = loaded[email];
                return;
            }

            if (requesting.ContainsKey(email)) {
                requesting[email].Add(this);
                return;
            }

            byte[] hash = MD5.Create().ComputeHash(Encoding.Default.GetBytes(email.ToLower().Trim()));
            string md5 = "";
            for (int i = 0; i < hash.Length; i++) md5 += hash[i].ToString("x2");
            md5 = md5.ToLower();

            string filePath = Path.Combine(CACHE_PATH, md5);
            if (File.Exists(filePath)) {
                var img = new BitmapImage(new Uri(filePath));
                loaded.Add(email, img);
                Source = img;
                return;
            }

            requesting.Add(email, new List<Avatar>());
            requesting[email].Add(this);

            Action job = () => {
                try {
                    HttpWebRequest req = WebRequest.CreateHttp(App.Setting.UI.AvatarServer + md5 + "?d=404");
                    req.Timeout = 2000;
                    req.Method = "GET";

                    HttpWebResponse rsp = req.GetResponse() as HttpWebResponse;
                    if (rsp.StatusCode == HttpStatusCode.OK) {
                        using (Stream reader = rsp.GetResponseStream())
                        using (FileStream writer = File.OpenWrite(filePath)) {
                            reader.CopyTo(writer);
                        }

                        if (requesting.ContainsKey(email)) {
                            Dispatcher.Invoke(() => {
                                var img = new BitmapImage(new Uri(filePath));
                                loaded[email] = img;
                                foreach (var one in requesting[email]) one.Source = img;
                            });
                        }
                    }
                } catch { }

                requesting.Remove(email);
            };

            if (loader != null && !loader.IsCompleted) {
                loader = loader.ContinueWith(t => { job(); });
            } else {
                loader = Task.Run(job);
            }
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
