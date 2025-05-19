using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace SourceGit.Views
{
    public class Avatar : Control, Models.IAvatarHost
    {
        public static readonly StyledProperty<Models.User> UserProperty =
            AvaloniaProperty.Register<Avatar, Models.User>(nameof(User));

        public Models.User User
        {
            get => GetValue(UserProperty);
            set => SetValue(UserProperty, value);
        }

        public Avatar()
        {
            var refetch = new MenuItem() { Header = App.Text("RefetchAvatar") };
            refetch.Click += (_, _) =>
            {
                if (User != null)
                    Models.AvatarManager.Instance.Request(User.Email, true);
            };

            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(refetch);

            RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.HighQuality);
        }

        public override void Render(DrawingContext context)
        {
            if (User == null)
                return;

            var corner = (float)Math.Max(2, Bounds.Width / 16);
            var rect = new Rect(0, 0, Bounds.Width, Bounds.Height);
            var clip = context.PushClip(new RoundedRect(rect, corner));

            if (_img != null)
            {
                context.DrawImage(_img, rect);
            }
            else
            {
                context.DrawRectangle(Brushes.White, new Pen(new SolidColorBrush(Colors.Black, 0.3f), 0.65f), rect, corner, corner);

                var offsetX = Bounds.Width / 10.0;
                var offsetY = Bounds.Height / 10.0;

                var stepX = (Bounds.Width - offsetX * 2) / 5.0;
                var stepY = (Bounds.Height - offsetY * 2) / 5.0;

                var user = User;
                var lowered = user.Email.ToLower(CultureInfo.CurrentCulture).Trim();
                var hash = MD5.HashData(Encoding.Default.GetBytes(lowered));

                var brush = new SolidColorBrush(new Color(255, hash[0], hash[1], hash[2]));
                var switches = new bool[15];
                for (int i = 0; i < switches.Length; i++)
                    switches[i] = hash[i + 1] % 2 == 1;

                for (int row = 0; row < 5; row++)
                {
                    var x = offsetX + stepX * 2;
                    var y = offsetY + stepY * row;
                    var idx = row * 3;

                    if (switches[idx])
                        context.FillRectangle(brush, new Rect(x, y, stepX, stepY));

                    if (switches[idx + 1])
                        context.FillRectangle(brush, new Rect(x + stepX, y, stepX, stepY));

                    if (switches[idx + 2])
                        context.FillRectangle(brush, new Rect(x + stepX * 2, y, stepX, stepY));
                }

                for (int row = 0; row < 5; row++)
                {
                    var x = offsetX;
                    var y = offsetY + stepY * row;
                    var idx = row * 3 + 2;

                    if (switches[idx])
                        context.FillRectangle(brush, new Rect(x, y, stepX, stepY));

                    if (switches[idx - 1])
                        context.FillRectangle(brush, new Rect(x + stepX, y, stepX, stepY));
                }
            }

            clip.Dispose();
        }

        public void OnAvatarResourceChanged(string email)
        {
            if (User.Email.Equals(email, StringComparison.Ordinal))
            {
                _img = Models.AvatarManager.Instance.Request(User.Email, false);
                InvalidateVisual();
            }
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            Models.AvatarManager.Instance.Subscribe(this);
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);
            Models.AvatarManager.Instance.Unsubscribe(this);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == UserProperty)
            {
                var user = User;
                if (user == null)
                    return;

                _img = Models.AvatarManager.Instance.Request(User.Email, false);
                InvalidateVisual();
            }
        }

        private Bitmap _img = null;
    }
}
