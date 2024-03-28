using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;

using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.TextMate;

namespace SourceGit.Views
{
    public class RevisionImageFileView : Control
    {
        public static readonly StyledProperty<Bitmap> SourceProperty =
            AvaloniaProperty.Register<ImageDiffView, Bitmap>(nameof(Source), null);

        public Bitmap Source
        {
            get => GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        static RevisionImageFileView()
        {
            AffectsMeasure<RevisionImageFileView>(SourceProperty);
        }

        public override void Render(DrawingContext context)
        {
            if (_bgBrush == null)
            {
                var maskBrush = new SolidColorBrush(ActualThemeVariant == ThemeVariant.Dark ? 0xFF404040 : 0xFFBBBBBB);
                var bg = new DrawingGroup()
                {
                    Children =
                    {
                        new GeometryDrawing() { Brush = maskBrush, Geometry = new RectangleGeometry(new Rect(0, 0, 12, 12)) },
                        new GeometryDrawing() { Brush = maskBrush, Geometry = new RectangleGeometry(new Rect(12, 12, 12, 12)) },
                    }
                };

                _bgBrush = new DrawingBrush(bg)
                {
                    AlignmentX = AlignmentX.Left,
                    AlignmentY = AlignmentY.Top,
                    DestinationRect = new RelativeRect(new Size(24, 24), RelativeUnit.Absolute),
                    Stretch = Stretch.None,
                    TileMode = TileMode.Tile,
                };
            }

            context.FillRectangle(_bgBrush, new Rect(Bounds.Size));

            var source = Source;
            if (source != null)
            {
                context.DrawImage(source, new Rect(source.Size), new Rect(8, 8, Bounds.Width - 16, Bounds.Height - 16));
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property.Name == "ActualThemeVariant")
            {
                _bgBrush = null;
                InvalidateVisual();
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var source = Source;
            if (source == null)
            {
                return availableSize;
            }

            var w = availableSize.Width - 16;
            var h = availableSize.Height - 16;
            var size = source.Size;
            if (size.Width <= w)
            {
                if (size.Height <= h)
                {
                    return new Size(size.Width + 16, size.Height + 16);
                }
                else
                {
                    return new Size(h * size.Width / size.Height + 16, availableSize.Height);
                }
            }
            else
            {
                var scale = Math.Max(size.Width / w, size.Height / h);
                return new Size(size.Width / scale + 16, size.Height / scale + 16);
            }
        }

        private DrawingBrush _bgBrush = null;
    }

    public class RevisionTextFileView : TextEditor
    {
        protected override Type StyleKeyOverride => typeof(TextEditor);

        public RevisionTextFileView() : base(new TextArea(), new TextDocument())
        {
            IsReadOnly = true;
            ShowLineNumbers = true;
            WordWrap = false;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

            TextArea.LeftMargins[0].Margin = new Thickness(8, 0);
            TextArea.TextView.Margin = new Thickness(4, 0);
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            TextArea.TextView.ContextRequested += OnTextViewContextRequested;

            _textMate = Models.TextMateHelper.CreateForEditor(this);
            if (DataContext is Models.RevisionTextFile source)
            {
                Models.TextMateHelper.SetGrammarByFileName(_textMate, source.FileName);
            }
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);

            TextArea.TextView.ContextRequested -= OnTextViewContextRequested;

            if (_textMate != null)
            {
                _textMate.Dispose();
                _textMate = null;
            }

            GC.Collect();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            var source = DataContext as Models.RevisionTextFile;
            if (source != null)
            {
                Text = source.Content;
                Models.TextMateHelper.SetGrammarByFileName(_textMate, source.FileName);
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property.Name == "ActualThemeVariant" && change.NewValue != null)
            {
                Models.TextMateHelper.SetThemeByApp(_textMate);
            }
        }

        private void OnTextViewContextRequested(object sender, ContextRequestedEventArgs e)
        {
            var selected = SelectedText;
            if (string.IsNullOrEmpty(selected)) return;

            var icon = new Avalonia.Controls.Shapes.Path();
            icon.Width = 10;
            icon.Height = 10;
            icon.Stretch = Stretch.Uniform;
            icon.Data = App.Current?.FindResource("Icons.Copy") as StreamGeometry;

            var copy = new MenuItem();
            copy.Header = App.Text("Copy");
            copy.Icon = icon;
            copy.Click += (o, ev) =>
            {
                App.CopyText(selected);
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(copy);
            menu.Open(TextArea.TextView);
            e.Handled = true;
        }

        private TextMate.Installation _textMate = null;
    }

    public partial class RevisionFiles : UserControl
    {
        public RevisionFiles()
        {
            InitializeComponent();
        }

        private void OnTreeViewContextRequested(object sender, ContextRequestedEventArgs e)
        {
            var detail = DataContext as ViewModels.CommitDetail;
            var node = detail.SelectedRevisionFileNode;
            if (!node.IsFolder)
            {
                var menu = detail.CreateRevisionFileContextMenu(node.Backend as Models.Object);
                menu.Open(sender as Control);
            }

            e.Handled = true;
        }
    }
}