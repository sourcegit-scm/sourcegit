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

        public static readonly StyledProperty<int> SubjectLengthProperty =
            AvaloniaProperty.Register<CommitMessageTextBox, int>(nameof(SubjectLength));
 
        public string Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public int SubjectLength
        {
            get => GetValue(SubjectLengthProperty);
            set => SetValue(SubjectLengthProperty, value);
        }

        public TextDocument Document
        {
            get;
            private set;
        }
        
        public CommitMessageTextBox()
        {
            Document = new TextDocument(Text);
            InitializeComponent();
        }
        
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == TextProperty)
            {
                if (!_isDocumentTextChanging)
                    Document.Text = Text;
            }
        }

        private void OnTextEditorLayoutUpdated(object sender, EventArgs e)
        {
            var view = TextEditor.TextArea?.TextView;
            if (view is { VisualLinesValid: true })
            {
                if (_subjectEndLineNumber == 0)
                {
                    SubjectGuideLine.Margin = new Thickness(1, view.DefaultLineHeight + 2, 1, 0);
                    SubjectGuideLine.IsVisible = true;
                    return;
                }
                
                foreach (var line in view.VisualLines)
                {
                    var lineNumber = line.FirstDocumentLine.LineNumber;
                    if (lineNumber == _subjectEndLineNumber)
                    {
                        var y = line.GetTextLineVisualYPosition(line.TextLines[^1], VisualYPosition.TextBottom) - view.VerticalOffset + 2;
                        SubjectGuideLine.Margin = new Thickness(1, y, 1, 0);
                        SubjectGuideLine.IsVisible = true;
                        return;
                    }
                }
            }
            
            SubjectGuideLine.IsVisible = false;
        }

        private void OnTextEditorTextChanged(object sender, EventArgs e)
        {
            _isDocumentTextChanging = true;
            SetCurrentValue(TextProperty, Document.Text);
            _isDocumentTextChanging = false;
            
            var setSubject = false;
            for (int i = 0; i < Document.LineCount; i++)
            {
                var line = Document.Lines[i];
                if (line.LineNumber > 1 && line.Length == 0)
                {
                    var subject = Text.Substring(0, line.Offset).ReplaceLineEndings(" ").Trim();
                    SetCurrentValue(SubjectLengthProperty, subject.Length);
                    setSubject = true;
                    break;
                }
                
                _subjectEndLineNumber = line.LineNumber;
            }

            if (setSubject)
                return;
            
            SetCurrentValue(SubjectLengthProperty, Text.ReplaceLineEndings(" ").Trim().Length);
        }

        private bool _isDocumentTextChanging = false;
        private int _subjectEndLineNumber = 0;
    }
}
