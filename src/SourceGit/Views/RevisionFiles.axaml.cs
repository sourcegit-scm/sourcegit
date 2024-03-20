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