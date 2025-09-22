using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

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

        public static readonly StyledProperty<bool> UseGitHubStyleAvatarProperty =
            AvaloniaProperty.Register<Avatar, bool>(nameof(UseGitHubStyleAvatar));

        public bool UseGitHubStyleAvatar
        {
            get => GetValue(UseGitHubStyleAvatarProperty);
            set => SetValue(UseGitHubStyleAvatarProperty, value);
        }

        public Avatar()
        {
            RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.HighQuality);

            this.Bind(UseGitHubStyleAvatarProperty, new Binding()
            {
                Mode = BindingMode.OneWay,
                Source = ViewModels.Preferences.Instance,
                Path = "UseGitHubStyleAvatar"
            });
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
            else if (!UseGitHubStyleAvatar)
            {
                var fallback = GetFallbackString(User.Name);
                var typeface = new Typeface("fonts:SourceGit#JetBrains Mono");
                var label = new FormattedText(
                    fallback,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    Math.Max(Bounds.Width * 0.65, 10),
                    Brushes.White);

                var chars = fallback.ToCharArray();
                var sum = 0;
                foreach (var c in chars)
                    sum += Math.Abs(c);

                var bg = new LinearGradientBrush()
                {
                    GradientStops = FALLBACK_GRADIENTS[sum % FALLBACK_GRADIENTS.Length],
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                };

                Point textOrigin = new Point((Bounds.Width - label.Width) * 0.5, (Bounds.Height - label.Height) * 0.5);
                context.DrawRectangle(bg, null, new Rect(0, 0, Bounds.Width, Bounds.Height), corner, corner);
                context.DrawText(label, textOrigin);
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

        public void OnAvatarResourceChanged(string email, Bitmap image)
        {
            if (email.Equals(User?.Email, StringComparison.Ordinal))
            {
                _img = image;
                InvalidateVisual();
            }
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            Models.AvatarManager.Instance.Subscribe(this);
            ContextRequested += OnContextRequested;
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);
            ContextRequested -= OnContextRequested;
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
            else if (change.Property == UseGitHubStyleAvatarProperty)
            {
                if (_img == null)
                    InvalidateVisual();
            }
        }

        private void OnContextRequested(object sender, ContextRequestedEventArgs e)
        {
            var toplevel = TopLevel.GetTopLevel(this);
            if (toplevel == null)
            {
                e.Handled = true;
                return;
            }

            var refetch = new MenuItem();
            refetch.Icon = App.CreateMenuIcon("Icons.Loading");
            refetch.Header = App.Text("Avatar.Refetch");
            refetch.Click += (_, ev) =>
            {
                if (User != null)
                    Models.AvatarManager.Instance.Request(User.Email, true);

                ev.Handled = true;
            };

            var load = new MenuItem();
            load.Icon = App.CreateMenuIcon("Icons.Folder.Open");
            load.Header = App.Text("Avatar.Load");
            load.Click += async (_, ev) =>
            {
                var options = new FilePickerOpenOptions()
                {
                    FileTypeFilter = [new FilePickerFileType("PNG") { Patterns = ["*.png"] }],
                    AllowMultiple = false,
                };

                var selected = await toplevel.StorageProvider.OpenFilePickerAsync(options);
                if (selected.Count == 1)
                {
                    var localFile = selected[0].Path.LocalPath;
                    Models.AvatarManager.Instance.SetFromLocal(User.Email, localFile);
                }

                ev.Handled = true;
            };

            var saveAs = new MenuItem();
            saveAs.Icon = App.CreateMenuIcon("Icons.Save");
            saveAs.Header = App.Text("SaveAs");
            saveAs.Click += async (_, ev) =>
            {
                var options = new FilePickerSaveOptions();
                options.Title = App.Text("SaveAs");
                options.DefaultExtension = ".png";
                options.FileTypeChoices = [new FilePickerFileType("PNG") { Patterns = ["*.png"] }];

                var storageFile = await toplevel.StorageProvider.SaveFilePickerAsync(options);
                if (storageFile != null)
                {
                    var saveTo = storageFile.Path.LocalPath;
                    await using (var writer = File.Create(saveTo))
                    {
                        if (_img != null)
                        {
                            _img.Save(writer);
                        }
                        else
                        {
                            var pixelSize = new PixelSize((int)Bounds.Width, (int)Bounds.Height);
                            var dpi = new Vector(96, 96);

                            using (var rt = new RenderTargetBitmap(pixelSize, dpi))
                            using (var ctx = rt.CreateDrawingContext())
                            {
                                Render(ctx);
                                rt.Save(writer);
                            }
                        }
                    }
                }

                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(refetch);
            menu.Items.Add(load);
            menu.Items.Add(new MenuItem() { Header = "-" });
            menu.Items.Add(saveAs);

            menu.Open(this);
        }

        private string GetFallbackString(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "?";

            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var chars = new List<char>();
            foreach (var part in parts)
                chars.Add(part[0]);

            if (chars.Count >= 2 && char.IsAsciiLetterOrDigit(chars[0]) && char.IsAsciiLetterOrDigit(chars[^1]))
                return string.Format("{0}{1}", chars[0], chars[^1]);

            return name.Substring(0, 1);
        }

        private static readonly GradientStops[] FALLBACK_GRADIENTS = [
            new GradientStops() { new GradientStop(Colors.Orange, 0), new GradientStop(Color.FromRgb(255, 213, 134), 1) },
            new GradientStops() { new GradientStop(Colors.DodgerBlue, 0), new GradientStop(Colors.LightSkyBlue, 1) },
            new GradientStops() { new GradientStop(Colors.LimeGreen, 0), new GradientStop(Color.FromRgb(124, 241, 124), 1) },
            new GradientStops() { new GradientStop(Colors.Orchid, 0), new GradientStop(Color.FromRgb(248, 161, 245), 1) },
            new GradientStops() { new GradientStop(Colors.Tomato, 0), new GradientStop(Color.FromRgb(252, 165, 150), 1) },
        ];

        private Bitmap _img = null;
    }
}
