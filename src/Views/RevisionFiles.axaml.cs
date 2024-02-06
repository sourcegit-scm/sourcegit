using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.TextMate;
using System;
using System.IO;
using TextMateSharp.Grammars;

namespace SourceGit.Views {

    public class RevisionTextFileView : TextEditor {
        protected override Type StyleKeyOverride => typeof(TextEditor);

        public RevisionTextFileView() : base(new TextArea(), new TextDocument()) {
            IsReadOnly = true;
            ShowLineNumbers = true;
            WordWrap = false;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

            TextArea.LeftMargins[0].Margin = new Thickness(8, 0);
            TextArea.TextView.Margin = new Thickness(4, 0);
        }

        protected override void OnLoaded(RoutedEventArgs e) {
            base.OnLoaded(e);

            TextArea.TextView.ContextRequested += OnTextViewContextRequested;
            if (App.Current?.ActualThemeVariant == ThemeVariant.Dark) {
                _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
            } else {
                _registryOptions = new RegistryOptions(ThemeName.LightPlus);
            }

            _textMate = this.InstallTextMate(_registryOptions);

            if (DataContext != null && DataContext is Models.RevisionTextFile source) {
                _textMate.SetGrammar(_registryOptions.GetScopeByExtension(Path.GetExtension(source.FileName)));
            }
        }

        protected override void OnUnloaded(RoutedEventArgs e) {
            base.OnUnloaded(e);

            TextArea.TextView.ContextRequested -= OnTextViewContextRequested;
            _registryOptions = null;
            _textMate.Dispose();
            _textMate = null;
            GC.Collect();
        }

        protected override void OnDataContextChanged(EventArgs e) {
            base.OnDataContextChanged(e);

            var source = DataContext as Models.RevisionTextFile;
            if (source != null) {
                if (_textMate != null) _textMate.SetGrammar(_registryOptions.GetScopeByExtension(Path.GetExtension(source.FileName)));
                Text = source.Content;
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
            base.OnPropertyChanged(change);

            if (change.Property.Name == "ActualThemeVariant" && change.NewValue != null && _textMate != null) {
                if (App.Current?.ActualThemeVariant == ThemeVariant.Dark) {
                    _textMate.SetTheme(_registryOptions.LoadTheme(ThemeName.DarkPlus));
                } else {
                    _textMate.SetTheme(_registryOptions.LoadTheme(ThemeName.LightPlus));
                }
            }
        }

        private void OnTextViewContextRequested(object sender, ContextRequestedEventArgs e) {
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
            copy.Click += (o, ev) => {
                App.CopyText(selected);
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(copy);
            menu.Open(TextArea.TextView);
            e.Handled = true;
        }

        private RegistryOptions _registryOptions = null;
        private TextMate.Installation _textMate = null;
    }

    public partial class RevisionFiles : UserControl {
        public RevisionFiles() {
            InitializeComponent();
        }

        private void OnTreeViewContextRequested(object sender, ContextRequestedEventArgs e) {
            var detail = DataContext as ViewModels.CommitDetail;
            var node = detail.SelectedRevisionFileNode;
            if (!node.IsFolder) {
                var menu = detail.CreateRevisionFileContextMenu(node.Backend as Models.Object);
                menu.Open(sender as Control);
            }

            e.Handled = true;
        }
    }
}
