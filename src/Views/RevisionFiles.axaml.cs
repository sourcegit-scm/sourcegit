using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
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
            TextArea.TextView.Options.EnableHyperlinks = false;
            TextArea.TextView.Options.EnableEmailHyperlinks = false;
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
                UpdateTextMate();
                Text = source.Content;
            }
            else
            {
                Text = string.Empty;
            }
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

        private void UpdateTextMate()
        {
            if (_textMate == null)
                _textMate = Models.TextMateHelper.CreateForEditor(this);

            if (DataContext is Models.RevisionTextFile file)
                Models.TextMateHelper.SetGrammarByFileName(_textMate, file.FileName);
        }

        private TextMate.Installation _textMate = null;
    }

    public partial class RevisionFiles : UserControl
    {
        public RevisionFiles()
        {
            InitializeComponent();
        }

        private void OnSearchBoxKeyDown(object _, KeyEventArgs e)
        {
            var vm = DataContext as ViewModels.CommitDetail;
            if (vm == null)
                return;

            if (e.Key == Key.Enter)
            {
                FileTree.SetSearchResult(vm.RevisionFileSearchFilter);
                e.Handled = true;
            }
            else if (e.Key == Key.Down || e.Key == Key.Up)
            {
                if (vm.IsRevisionFileSearchSuggestionOpen)
                {
                    SearchSuggestionBox.Focus(NavigationMethod.Tab);
                    SearchSuggestionBox.SelectedIndex = 0;
                }

                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (vm.IsRevisionFileSearchSuggestionOpen)
                {
                    vm.RevisionFileSearchSuggestion.Clear();
                    vm.IsRevisionFileSearchSuggestionOpen = false;
                }

                e.Handled = true;
            }
        }

        private void OnSearchBoxTextChanged(object _, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(TxtSearchRevisionFiles.Text))
                FileTree.SetSearchResult(null);
        }

        private void OnSearchSuggestionBoxKeyDown(object _, KeyEventArgs e)
        {
            var vm = DataContext as ViewModels.CommitDetail;
            if (vm == null)
                return;

            if (e.Key == Key.Escape)
            {
                vm.RevisionFileSearchSuggestion.Clear();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && SearchSuggestionBox.SelectedItem is string content)
            {
                vm.RevisionFileSearchFilter = content;
                TxtSearchRevisionFiles.CaretIndex = content.Length;
                FileTree.SetSearchResult(vm.RevisionFileSearchFilter);
                e.Handled = true;
            }
        }

        private void OnSearchSuggestionDoubleTapped(object sender, TappedEventArgs e)
        {
            var vm = DataContext as ViewModels.CommitDetail;
            if (vm == null)
                return;

            var content = (sender as StackPanel)?.DataContext as string;
            if (!string.IsNullOrEmpty(content))
            {
                vm.RevisionFileSearchFilter = content;
                TxtSearchRevisionFiles.CaretIndex = content.Length;
                FileTree.SetSearchResult(vm.RevisionFileSearchFilter);
            }

            e.Handled = true;
        }
    }
}
