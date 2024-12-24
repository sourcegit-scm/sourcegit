using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.TextMate;
using AvaloniaEdit;

namespace SourceGit.Views
{
    public class AIResponseView : TextEditor
    {
        protected override Type StyleKeyOverride => typeof(TextEditor);

        public AIResponseView() : base(new TextArea(), new TextDocument())
        {
            IsReadOnly = true;
            ShowLineNumbers = false;
            WordWrap = true;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
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

        private void OnTextViewContextRequested(object sender, ContextRequestedEventArgs e)
        {
            var selected = SelectedText;
            if (string.IsNullOrEmpty(selected))
                return;

            var copy = new MenuItem() { Header = App.Text("Copy") };
            copy.Click += (_, ev) =>
            {
                App.CopyText(selected);
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

        private TextMate.Installation _textMate = null;
    }

    public partial class AIAssistant : ChromelessWindow
    {
        public AIAssistant()
        {
            InitializeComponent();
        }

        public AIAssistant(Models.OpenAIService service, string repo, ViewModels.WorkingCopy wc, List<Models.Change> changes)
        {
            _service = service;
            _repo = repo;
            _wc = wc;
            _changes = changes;

            InitializeComponent();
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            Generate();
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);
            _cancel?.Cancel();
        }

        private void OnGenerateCommitMessage(object sender, RoutedEventArgs e)
        {
            if (_wc != null)
                _wc.CommitMessage = TxtResponse.Text;

            Close();
        }

        private void OnRegen(object sender, RoutedEventArgs e)
        {
            TxtResponse.Text = string.Empty;
            Generate();
            e.Handled = true;
        }

        private void Generate()
        {
            if (_repo == null)
                return;

            IconInProgress.IsVisible = true;
            BtnGenerateCommitMessage.IsEnabled = false;
            BtnRegenerate.IsEnabled = false;

            _cancel = new CancellationTokenSource();
            Task.Run(() =>
            {
                new Commands.GenerateCommitMessage(_service, _repo, _changes, _cancel.Token, message =>
                {
                    Dispatcher.UIThread.Invoke(() => TxtResponse.Text = message);
                }).Exec();

                if (!_cancel.IsCancellationRequested)
                {
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        IconInProgress.IsVisible = false;
                        BtnGenerateCommitMessage.IsEnabled = true;
                        BtnRegenerate.IsEnabled = true;
                    });
                }
            }, _cancel.Token);
        }

        private Models.OpenAIService _service;
        private string _repo;
        private ViewModels.WorkingCopy _wc;
        private List<Models.Change> _changes;
        private CancellationTokenSource _cancel;
    }
}
