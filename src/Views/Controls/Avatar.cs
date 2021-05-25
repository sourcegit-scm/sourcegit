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

namespace SourceGit.Views.Controls {

    /// <summary>
    ///     头像控件
    /// </summary>
    public class Avatar : Image {

        /// <summary>
        ///     显示FallbackLabel时的背景色
        /// </summary>
        private static readonly Brush[] BACKGROUND_BRUSHES = new Brush[] {
            new LinearGradientBrush(Colors.Orange, Color.FromRgb(255, 213, 134), 90),
            new LinearGradientBrush(Colors.DodgerBlue, Colors.LightSkyBlue, 90),
            new LinearGradientBrush(Colors.LimeGreen, Color.FromRgb(124, 241, 124), 90),
            new LinearGradientBrush(Colors.Orchid, Color.FromRgb(248, 161, 245), 90),
            new LinearGradientBrush(Colors.Tomato, Color.FromRgb(252, 165, 150), 90),
        };

        /// <summary>
        ///     头像资源本地缓存路径
        /// </summary>
        public static readonly string CACHE_PATH = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SourceGit",
            "avatars");

        /// <summary>
        ///     邮件属性定义
        /// </summary>
        public static readonly DependencyProperty EmailProperty = DependencyProperty.Register(
            "Email", 
            typeof(string),
            typeof(Avatar), 
            new PropertyMetadata(null, OnEmailChanged));

        /// <summary>
        ///     邮件属性
        /// </summary>
        public string Email {
            get { return (string)GetValue(EmailProperty); }
            set { SetValue(EmailProperty, value); }
        }

        /// <summary>
        ///     下载头像失败时显示的Label属性定义
        /// </summary>
        public static readonly DependencyProperty FallbackLabelProperty = DependencyProperty.Register(
            "FallbackLabel",
            typeof(string),
            typeof(Avatar),
            new PropertyMetadata("?", OnFallbackLabelChanged));

        /// <summary>
        ///     下载头像失败时显示的Label属性
        /// </summary>
        public string FallbackLabel {
            get { return (string)GetValue(FallbackLabelProperty); }
            set { SetValue(FallbackLabelProperty, value); }
        }

        private static Dictionary<string, List<Avatar>> requesting = new Dictionary<string, List<Avatar>>();
        private static Dictionary<string, BitmapImage> loaded = new Dictionary<string, BitmapImage>();
        private static Task loader = null;

        private int colorIdx = 0;
        private FormattedText label = null;

        public Avatar() {
            SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
        }

        /// <summary>
        ///     渲染实现
        /// </summary>
        /// <param name="dc"></param>
        protected override void OnRender(DrawingContext dc) {
            base.OnRender(dc);

            if (Source == null && label != null) {
                var corner = Math.Max(2, Width / 16);
                var offsetX = (double)0;
                if (HorizontalAlignment == HorizontalAlignment.Right) {
                    offsetX = -Width * 0.5;
                } else if (HorizontalAlignment == HorizontalAlignment.Left) {
                    offsetX = Width * 0.5;
                }

                Brush brush = BACKGROUND_BRUSHES[colorIdx];
                dc.DrawRoundedRectangle(brush, null, new Rect(-Width * 0.5 + offsetX, -Height * 0.5, Width, Height), corner, corner);
                dc.DrawText(label, new Point(label.Width * -0.5 + offsetX, label.Height * -0.5));
            }
        }

        /// <summary>
        ///     显示文本变化时触发
        /// </summary>
        /// <param name="d"></param>
        /// <param name="e"></param>
        private static void OnFallbackLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            Avatar a = d as Avatar;
            if (a == null) return;

            var placeholder = a.FallbackLabel.Length > 0 ? a.FallbackLabel.Substring(0, 1) : "?";

            a.colorIdx = 0;
            a.label = new FormattedText(
                placeholder,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                a.Width * 0.65,
                Brushes.White,
                VisualTreeHelper.GetDpi(a).PixelsPerDip);

            var chars = placeholder.ToCharArray();
            foreach (var ch in chars) a.colorIdx += Math.Abs(ch);
            a.colorIdx = a.colorIdx % BACKGROUND_BRUSHES.Length;
        }

        /// <summary>
        ///     邮件变化时触发
        /// </summary>
        /// <param name="d"></param>
        /// <param name="e"></param>
        private static void OnEmailChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            Avatar a = d as Avatar;
            if (a == null) return;

            var oldEmail = e.OldValue as string;
            if (!string.IsNullOrEmpty(oldEmail) && requesting.ContainsKey(oldEmail)) {
                if (requesting[oldEmail].Count <= 1) {
                    requesting.Remove(oldEmail);
                } else {
                    requesting[oldEmail].Remove(a);
                }
            }

            a.Source = null;
            a.InvalidateVisual();

            var email = e.NewValue as string;
            if (string.IsNullOrEmpty(email)) return;

            if (loaded.ContainsKey(email)) {
                a.Source = loaded[email];
                return;
            }

            if (requesting.ContainsKey(email)) {
                requesting[email].Add(a);
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
                a.Source = img;
                return;
            }

            requesting.Add(email, new List<Avatar>());
            requesting[email].Add(a);

            Action job = () => {
                try {
                    HttpWebRequest req = WebRequest.CreateHttp(Models.Preference.Instance.General.AvatarServer + md5 + "?d=404");
                    req.Timeout = 2000;
                    req.Method = "GET";

                    HttpWebResponse rsp = req.GetResponse() as HttpWebResponse;
                    if (rsp.StatusCode == HttpStatusCode.OK) {
                        using (Stream reader = rsp.GetResponseStream())
                        using (FileStream writer = File.OpenWrite(filePath)) {
                            reader.CopyTo(writer);
                        }

                        a.Dispatcher.Invoke(() => {
                            var img = new BitmapImage(new Uri(filePath));
                            loaded.Add(email, img);

                            if (requesting.ContainsKey(email)) {
                                foreach (var one in requesting[email]) one.Source = img;
                            }
                        });
                    } else {
                        if (!loaded.ContainsKey(email)) loaded.Add(email, null);
                    }
                } catch {
                    if (!loaded.ContainsKey(email)) loaded.Add(email, null);
                }

                requesting.Remove(email);
            };

            if (loader != null && !loader.IsCompleted) {
                loader = loader.ContinueWith(t => { job(); });
            } else {
                loader = Task.Run(job);
            }
        }
    }
}
