using System;

using Avalonia;
using Avalonia.Controls;

using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace SourceGit.Views
{
    public partial class CommitMessageTextBox : UserControl
    {
        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<CommitMessageTextBox, string>(nameof(Text), string.Empty);

        public static readonly DirectProperty<CommitMessageTextBox, int> SubjectLengthProperty =
            AvaloniaProperty.RegisterDirect<CommitMessageTextBox, int>(nameof(SubjectLength), o => o.SubjectLength);

        public static readonly DirectProperty<CommitMessageTextBox, int> TotalLengthProperty =
            AvaloniaProperty.RegisterDirect<CommitMessageTextBox, int>(nameof(TotalLength), o => o.TotalLength);
 
        public string Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public int SubjectLength
        {
            get => _subjectLength;
            private set => SetAndRaise(SubjectLengthProperty, ref _subjectLength, value);
        }

        public int TotalLength
        {
            get => _totalLength;
            private set => SetAndRaise(TotalLengthProperty, ref _totalLength, value);
        }

        public TextDocument Document
        {
            get;
        }
        
        public CommitMessageTextBox()
        {
            Document = new TextDocument(Text);
            InitializeComponent();
        }
        
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == TextProperty && !_isDocumentTextChanging)
                    Document.Text = Text;
        }

        private void OnTextEditorLayoutUpdated(object sender, EventArgs e)
        {
            var view = TextEditor.TextArea?.TextView;
            if (view is { VisualLinesValid: true })
            {
                if (_subjectEndLineNumber == 0)
                {
                    SubjectGuideLine.Margin = new Thickness(0, view.DefaultLineHeight + 3, 0, 0);
                    SubjectGuideLine.IsVisible = true;
                    return;
                }
                
                foreach (var line in view.VisualLines)
                {
                    var lineNumber = line.FirstDocumentLine.LineNumber;
                    if (lineNumber == _subjectEndLineNumber)
                    {
                        var y = line.GetTextLineVisualYPosition(line.TextLines[^1], VisualYPosition.LineBottom) - view.VerticalOffset + 3;
                        SubjectGuideLine.Margin = new Thickness(0, y, 0, 0);
                        SubjectGuideLine.IsVisible = true;
                        return;
                    }
                }
            }
            
            SubjectGuideLine.IsVisible = false;
        }

        private void OnTextEditorTextChanged(object sender, EventArgs e)
        {
            var text = Document.Text;
            _isDocumentTextChanging = true;
            SetCurrentValue(TextProperty, text);
            TotalLength = text.Trim().Length;
            _isDocumentTextChanging = false;

            var foundData = false;
            for (var i = 0; i < Document.LineCount; i++)
            {
                var line = Document.Lines[i];
                if (line.Length == 0)
                {
                    if (foundData)
                    {
                        SubjectLength = text[..line.Offset].ReplaceLineEndings(" ").Trim().Length;
                        return;
                    }
                }
                else
                {
                    foundData = true;
                }
                
                _subjectEndLineNumber = line.LineNumber;
            }
            
            SubjectLength = text.ReplaceLineEndings(" ").Trim().Length;
        }

        private bool _isDocumentTextChanging = false;
        private int _subjectEndLineNumber = 0;
        private int _totalLength = 0;
        private int _subjectLength = 0;
    }
}
