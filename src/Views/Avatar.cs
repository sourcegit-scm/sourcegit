using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using Avalonia;
using Avalonia.Controls;
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

        public Avatar()
        {
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

        public void OnAvatarResourceChanged(string email, Bitmap image)
        {
            if (User.Email.Equals(email, StringComparison.Ordinal))
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
                    await using (var writer = File.OpenWrite(saveTo))
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

        private Bitmap _img = null;
    }
}
