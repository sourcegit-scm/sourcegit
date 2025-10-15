using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;

using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.TextMate;

namespace SourceGit.Views
{
    public class RevisionTextFileView : TextEditor
    {
        public static readonly StyledProperty<int> TabWidthProperty =
            AvaloniaProperty.Register<RevisionTextFileView, int>(nameof(TabWidth), 4);

        public int TabWidth
        {
            get => GetValue(TabWidthProperty);
            set => SetValue(TabWidthProperty, value);
        }

        public static readonly StyledProperty<bool> UseSyntaxHighlightingProperty =
            AvaloniaProperty.Register<RevisionTextFileView, bool>(nameof(UseSyntaxHighlighting));

        public bool UseSyntaxHighlighting
        {
            get => GetValue(UseSyntaxHighlightingProperty);
            set => SetValue(UseSyntaxHighlightingProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(TextEditor);

        public RevisionTextFileView() : base(new TextArea(), new TextDocument())
        {
            IsReadOnly = true;
            ShowLineNumbers = true;
            WordWrap = false;

            Options.IndentationSize = TabWidth;
            Options.EnableHyperlinks = false;
            Options.EnableEmailHyperlinks = false;

            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

            TextArea.LeftMargins[0].Margin = new Thickness(8, 0);
            TextArea.TextView.Margin = new Thickness(4, 0);
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            TextArea.TextView.ContextRequested += OnTextViewContextRequested;
            UpdateTextMate();
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

            if (DataContext is Models.RevisionTextFile source)
            {
                Text = source.Content;
                Models.TextMateHelper.SetGrammarByFileName(_textMate, source.FileName);
                ScrollToHome();
            }
            else
            {
                Text = string.Empty;
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == TabWidthProperty)
                Options.IndentationSize = TabWidth;
            else if (change.Property == UseSyntaxHighlightingProperty)
                UpdateTextMate();
        }

        private void OnTextViewContextRequested(object sender, ContextRequestedEventArgs e)
        {
            var selected = SelectedText;
            if (string.IsNullOrEmpty(selected))
                return;

            var copy = new MenuItem() { Header = App.Text("Copy") };
            copy.Click += async (_, ev) =>
            {
                await App.CopyTextAsync(selected);
                ev.Handled = true;
            };

            if (this.FindResource("Icons.Copy") is Geometry geo)
            {
                copy.Icon = new Avalonia.Controls.Shapes.Path()
                {
                    Width = 10,
                    Height = 10,
                    Stretch = Stretch.Uniform,
                    Data = geo,
                };
            }

            var menu = new ContextMenu();
            menu.Items.Add(copy);
            menu.Open(TextArea.TextView);

            e.Handled = true;
        }

        private void UpdateTextMate()
        {
            if (UseSyntaxHighlighting)
            {
                _textMate ??= Models.TextMateHelper.CreateForEditor(this);

                if (DataContext is Models.RevisionTextFile file)
                    Models.TextMateHelper.SetGrammarByFileName(_textMate, file.FileName);
            }
            else if (_textMate != null)
            {
                _textMate.Dispose();
                _textMate = null;
                GC.Collect();

                TextArea.TextView.Redraw();
            }
        }

        private TextMate.Installation _textMate = null;
    }

    public partial class RevisionFileContentViewer : UserControl
    {
        public RevisionFileContentViewer()
        {
            InitializeComponent();
        }
    }
}
