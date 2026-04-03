using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;

using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;

namespace SourceGit.Views
{
    public class AIResponseView : TextEditor
    {
        public class LineStyleTransformer : DocumentColorizingTransformer
        {
            protected override void ColorizeLine(DocumentLine line)
            {
                var content = CurrentContext.Document.GetText(line);
                if (content.StartsWith("Read changes in file: ", StringComparison.Ordinal))
                {
                    ChangeLinePart(line.Offset + 22, line.EndOffset, v =>
                    {
                        v.TextRunProperties.SetForegroundBrush(Brushes.DeepSkyBlue);
                        v.TextRunProperties.SetTextDecorations(TextDecorations.Underline);
                    });
                }
            }
        }

        public static readonly StyledProperty<string> ContentProperty =
            AvaloniaProperty.Register<AIResponseView, string>(nameof(Content), string.Empty);

        public string Content
        {
            get => GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(TextEditor);

        public AIResponseView() : base(new TextArea(), new TextDocument())
        {
            IsReadOnly = true;
            ShowLineNumbers = false;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

            TextArea.TextView.Margin = new Thickness(4, 0);
            TextArea.TextView.Options.EnableHyperlinks = false;
            TextArea.TextView.Options.EnableEmailHyperlinks = false;
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            TextArea.TextView.ContextRequested += OnTextViewContextRequested;

            if (_textMate == null)
            {
                _textMate = Models.TextMateHelper.CreateForEditor(this);
                Models.TextMateHelper.SetGrammarByFileName(_textMate, "README.md");
                TextArea.TextView.LineTransformers.Add(new LineStyleTransformer());
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

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ContentProperty)
                Text = Content;
        }

        private void OnTextViewContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is not ViewModels.AIAssistant vm)
                return;

            var selected = SelectedText;
            if (string.IsNullOrEmpty(selected))
                return;

            var apply = new MenuItem() { Header = App.Text("AIAssistant.Use") };
            apply.Icon = this.CreateMenuIcon("Icons.Check");
            apply.Click += (_, ev) =>
            {
                vm.Use(selected);
                ev.Handled = true;
            };

            var copy = new MenuItem() { Header = App.Text("Copy") };
            copy.Icon = this.CreateMenuIcon("Icons.Copy");
            copy.Click += async (_, ev) =>
            {
                await this.CopyTextAsync(selected);
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(apply);
            menu.Items.Add(copy);
            menu.Open(TextArea.TextView);

            e.Handled = true;
        }

        private TextMate.Installation _textMate = null;
    }

    public partial class AIAssistant : ChromelessWindow
    {
        public AIAssistant()
        {
            CloseOnESC = true;
            InitializeComponent();
        }

        protected override async void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            if (DataContext is ViewModels.AIAssistant vm)
                await vm.GenAsync();
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);
            (DataContext as ViewModels.AIAssistant)?.Cancel();
        }

        private async void OnModelChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ViewModels.AIAssistant vm && IsLoaded)
                await vm.GenAsync();
            e.Handled = true;
        }

        private async void OnRegenClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.AIAssistant vm)
                await vm.GenAsync();

            e.Handled = true;
        }
    }
}
