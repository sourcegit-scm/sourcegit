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
    public class LogContentPresenter : TextEditor
    {
        public class LineStyleTransformer : DocumentColorizingTransformer
        {
            protected override void ColorizeLine(DocumentLine line)
            {
                var content = CurrentContext.Document.GetText(line);
                if (content.StartsWith("$ git ", StringComparison.Ordinal))
                {
                    ChangeLinePart(line.Offset, line.Offset + 1, v =>
                    {
                        v.TextRunProperties.SetForegroundBrush(Brushes.Orange);
                    });

                    ChangeLinePart(line.Offset + 2, line.EndOffset, v =>
                    {
                        var old = v.TextRunProperties.Typeface;
                        v.TextRunProperties.SetTypeface(new Typeface(old.FontFamily, old.Style, FontWeight.Bold));
                    });
                }
            }
        }

        public static readonly StyledProperty<ViewModels.CommandLog> LogProperty =
            AvaloniaProperty.Register<LogContentPresenter, ViewModels.CommandLog>(nameof(Log));

        public ViewModels.CommandLog Log
        {
            get => GetValue(LogProperty);
            set => SetValue(LogProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(TextEditor);

        public LogContentPresenter() : base(new TextArea(), new TextDocument())
        {
            IsReadOnly = true;
            ShowLineNumbers = false;
            WordWrap = false;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

            TextArea.TextView.Margin = new Thickness(4, 0);
            TextArea.TextView.Options.EnableHyperlinks = false;
            TextArea.TextView.Options.EnableEmailHyperlinks = false;
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            if (_textMate == null)
            {
                _textMate = Models.TextMateHelper.CreateForEditor(this);
                Models.TextMateHelper.SetGrammarByFileName(_textMate, "Log.log");
                TextArea.TextView.LineTransformers.Add(new LineStyleTransformer());
            }
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);

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

            if (change.Property == LogProperty)
            {
                if (change.NewValue is ViewModels.CommandLog log)
                {
                    Text = log.Content;
                    log.Register(OnLogLineReceived);
                }
                else
                {
                    Text = string.Empty;
                }
            }
        }

        private void OnLogLineReceived(string newline)
        {
            AppendText("\n");
            AppendText(newline);
        }

        private TextMate.Installation _textMate = null;
    }

    public partial class ViewLogs : ChromelessWindow
    {
        public ViewLogs()
        {
            InitializeComponent();
        }

        private void OnLogContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is not Grid { DataContext: ViewModels.CommandLog log } grid || DataContext is not ViewModels.ViewLogs vm)
                return;

            var copy = new MenuItem();
            copy.Header = App.Text("ViewLogs.CopyLog");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += (_, ev) =>
            {
                App.CopyText(log.Content);
                ev.Handled = true;
            };

            var rm = new MenuItem();
            rm.Header = App.Text("ViewLogs.Delete");
            rm.Icon = App.CreateMenuIcon("Icons.Clear");
            rm.Click += (_, ev) =>
            {
                vm.Logs.Remove(log);
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(copy);
            menu.Items.Add(rm);
            menu.Open(grid);

            e.Handled = true;
        }
    }
}
