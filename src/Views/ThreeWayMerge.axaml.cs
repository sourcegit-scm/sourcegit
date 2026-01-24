using System;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;

namespace SourceGit.Views
{
    public class MergeTextEditor : TextEditor
    {
        public static readonly StyledProperty<string> FilePathProperty =
            AvaloniaProperty.Register<MergeTextEditor, string>(nameof(FilePath));

        public string FilePath
        {
            get => GetValue(FilePathProperty);
            set => SetValue(FilePathProperty, value);
        }

        public static readonly StyledProperty<bool> HighlightConflictMarkersProperty =
            AvaloniaProperty.Register<MergeTextEditor, bool>(nameof(HighlightConflictMarkers), false);

        public bool HighlightConflictMarkers
        {
            get => GetValue(HighlightConflictMarkersProperty);
            set => SetValue(HighlightConflictMarkersProperty, value);
        }

        public static readonly StyledProperty<string> ContentProperty =
            AvaloniaProperty.Register<MergeTextEditor, string>(nameof(Content), string.Empty);

        public string Content
        {
            get => GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(TextEditor);

        public MergeTextEditor() : base(new TextArea(), new TextDocument())
        {
            ShowLineNumbers = true;
            WordWrap = false;
            Options.EnableHyperlinks = false;
            Options.EnableEmailHyperlinks = false;

            _textMate = Models.TextMateHelper.CreateForEditor(this);

            TextArea.TextView.Margin = new Thickness(4, 0);
            TextArea.TextView.ContextRequested += OnTextViewContextRequested;
        }

        public void ScrollToConflictLine(int lineNumber)
        {
            if (lineNumber > 0 && lineNumber <= Document.LineCount)
            {
                var line = Document.GetLineByNumber(lineNumber);
                ScrollTo(lineNumber, 0);
                TextArea.Caret.Offset = line.Offset;
            }
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (!HighlightConflictMarkers || string.IsNullOrEmpty(Text))
                return;

            var view = TextArea.TextView;
            if (view is not { VisualLinesValid: true })
                return;

            var conflictStartBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(60, 0, 200, 0));
            var conflictSeparatorBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(60, 200, 200, 0));
            var conflictEndBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(60, 200, 100, 0));
            var markerBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(80, 255, 0, 0));

            foreach (var line in view.VisualLines)
            {
                if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted)
                    continue;

                var lineNumber = line.FirstDocumentLine.LineNumber;
                var docLine = Document.GetLineByNumber(lineNumber);
                var lineText = Document.GetText(docLine.Offset, docLine.Length);

                Avalonia.Media.IBrush brush = null;

                if (lineText.StartsWith("<<<<<<<", StringComparison.Ordinal))
                    brush = conflictStartBrush;
                else if (lineText.StartsWith("|||||||", StringComparison.Ordinal))
                    brush = markerBrush;
                else if (lineText.StartsWith("=======", StringComparison.Ordinal))
                    brush = conflictSeparatorBrush;
                else if (lineText.StartsWith(">>>>>>>", StringComparison.Ordinal))
                    brush = conflictEndBrush;

                if (brush != null)
                {
                    var startY = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineTop) - view.VerticalOffset;
                    var endY = line.GetTextLineVisualYPosition(line.TextLines[^1], VisualYPosition.LineBottom) - view.VerticalOffset;
                    context.FillRectangle(brush, new Rect(0, startY, Bounds.Width, endY - startY));
                }
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
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == FilePathProperty)
            {
                if (FilePath is { Length: > 0 })
                    Models.TextMateHelper.SetGrammarByFileName(_textMate, FilePath);
            }
            else if (change.Property == ContentProperty)
            {
                Text = Content ?? string.Empty;
            }
            else if (change.Property.Name == nameof(ActualThemeVariant) && change.NewValue != null)
            {
                Models.TextMateHelper.SetThemeByApp(_textMate);
            }
        }

        private void OnTextViewContextRequested(object sender, ContextRequestedEventArgs e)
        {
            var selected = SelectedText;
            if (string.IsNullOrEmpty(selected))
                return;

            var copy = new MenuItem();
            copy.Header = App.Text("Copy");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += async (_, ev) =>
            {
                await App.CopyTextAsync(selected);
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(copy);
            menu.Open(TextArea.TextView);

            e.Handled = true;
        }

        private TextMate.Installation _textMate = null;
    }

    public partial class ThreeWayMerge : ChromelessWindow
    {
        public ThreeWayMerge()
        {
            InitializeComponent();
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            // Set up scroll synchronization for read-only editors
            SetupScrollSync();

            // Set up text change tracking for result editor
            SetupResultEditorBinding();
        }

        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);

            if (DataContext is ViewModels.ThreeWayMerge vm && vm.HasUnsavedChanges())
            {
                e.Cancel = true;
                var result = await App.AskConfirmAsync(App.Text("ThreeWayMerge.UnsavedChanges"));
                if (result)
                {
                    Close();
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            GC.Collect();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Handled)
                return;

            var vm = DataContext as ViewModels.ThreeWayMerge;
            if (vm == null)
                return;

            var modifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

            if (e.KeyModifiers == modifier)
            {
                // Ctrl+S to save
                if (e.Key == Key.S && vm.CanSave)
                {
                    _ = SaveAndCloseAsync();
                    e.Handled = true;
                }
                // Ctrl+Up to go to previous conflict
                else if (e.Key == Key.Up && vm.HasPrevConflict)
                {
                    vm.GotoPrevConflict();
                    ScrollToCurrentConflict();
                    e.Handled = true;
                }
                // Ctrl+Down to go to next conflict
                else if (e.Key == Key.Down && vm.HasNextConflict)
                {
                    vm.GotoNextConflict();
                    ScrollToCurrentConflict();
                    e.Handled = true;
                }
            }
        }

        private void OnAcceptMine(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.ThreeWayMerge vm)
            {
                vm.AcceptOurs();
            }
            e.Handled = true;
        }

        private void OnAcceptTheirs(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.ThreeWayMerge vm)
            {
                vm.AcceptTheirs();
            }
            e.Handled = true;
        }

        private async void OnSaveAndStage(object sender, RoutedEventArgs e)
        {
            await SaveAndCloseAsync();
            e.Handled = true;
        }

        private async Task SaveAndCloseAsync()
        {
            if (DataContext is ViewModels.ThreeWayMerge vm)
            {
                var success = await vm.SaveAndStageAsync();
                if (success)
                {
                    Close();
                }
            }
        }

        private void SetupScrollSync()
        {
            // Sync scrolling between Base, Ours, and Theirs editors
            var oursEditor = this.FindControl<MergeTextEditor>("OursEditor");
            var theirsEditor = this.FindControl<MergeTextEditor>("TheirsEditor");

            if (oursEditor != null && theirsEditor != null)
            {
                oursEditor.TextArea.TextView.ScrollOffsetChanged += (s, e) =>
                {
                    if (!_isSyncing)
                    {
                        _isSyncing = true;
                        theirsEditor.ScrollToVerticalOffset(oursEditor.VerticalOffset);
                        _isSyncing = false;
                    }
                };

                theirsEditor.TextArea.TextView.ScrollOffsetChanged += (s, e) =>
                {
                    if (!_isSyncing)
                    {
                        _isSyncing = true;
                        oursEditor.ScrollToVerticalOffset(theirsEditor.VerticalOffset);
                        _isSyncing = false;
                    }
                };
            }
        }

        private void SetupResultEditorBinding()
        {
            var resultEditor = this.FindControl<MergeTextEditor>("ResultEditor");
            if (resultEditor != null)
            {
                resultEditor.TextChanged += (s, e) =>
                {
                    if (DataContext is ViewModels.ThreeWayMerge vm && !_isUpdatingContent)
                    {
                        _isUpdatingContent = true;
                        vm.ResultContent = resultEditor.Text;
                        _isUpdatingContent = false;
                    }
                };
            }
        }

        private void ScrollToCurrentConflict()
        {
            if (DataContext is ViewModels.ThreeWayMerge vm && vm.CurrentConflictLine >= 0)
            {
                var resultEditor = this.FindControl<MergeTextEditor>("ResultEditor");
                resultEditor?.ScrollToConflictLine(vm.CurrentConflictLine + 1);
            }
        }

        private bool _isSyncing = false;
        private bool _isUpdatingContent = false;
    }
}
