using System;
using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.TextMate;

namespace SourceGit.Views
{
    public class LogContentPresenter : TextEditor
    {
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
            TextArea.TextView.Options.EnableHyperlinks = true;
            TextArea.TextView.Options.EnableEmailHyperlinks = true;
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            if (_textMate == null)
            {
                _textMate = Models.TextMateHelper.CreateForEditor(this);
                Models.TextMateHelper.SetGrammarByFileName(_textMate, "Log.log");
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
