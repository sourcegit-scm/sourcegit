using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public class EnhancedTextBox : TextBox
    {
        public static readonly RoutedEvent<KeyEventArgs> PreviewKeyDownEvent =
            RoutedEvent.Register<ChangeCollectionView, KeyEventArgs>(nameof(KeyEventArgs), RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

        public event EventHandler<KeyEventArgs> PreviewKeyDown
        {
            add { AddHandler(PreviewKeyDownEvent, value); }
            remove { RemoveHandler(PreviewKeyDownEvent, value); }
        }

        protected override Type StyleKeyOverride => typeof(TextBox);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            var dump = new KeyEventArgs()
            {
                RoutedEvent = PreviewKeyDownEvent,
                Route = RoutingStrategies.Direct,
                Source = e.Source,
                Key = e.Key,
                KeyModifiers = e.KeyModifiers,
                PhysicalKey = e.PhysicalKey,
                KeySymbol = e.KeySymbol,
            };

            RaiseEvent(dump);

            if (dump.Handled)
                e.Handled = true;
            else
                base.OnKeyDown(e);
        }
    }

    public partial class CommitMessageTextBox : UserControl
    {
        public enum TextChangeWay
        {
            None,
            FromSource,
            FromEditor,
        }

        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<CommitMessageTextBox, string>(nameof(Text), string.Empty);

        public static readonly StyledProperty<string> SubjectProperty =
            AvaloniaProperty.Register<CommitMessageTextBox, string>(nameof(Subject), string.Empty);

        public static readonly StyledProperty<string> DescriptionProperty =
            AvaloniaProperty.Register<CommitMessageTextBox, string>(nameof(Description), string.Empty);

        public string Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public string Subject
        {
            get => GetValue(SubjectProperty);
            set => SetValue(SubjectProperty, value);
        }

        public string Description
        {
            get => GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public CommitMessageTextBox()
        {
            InitializeComponent();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == TextProperty && _changingWay == TextChangeWay.None)
            {
                _changingWay = TextChangeWay.FromSource;
                var normalized = Text.ReplaceLineEndings("\n").Trim();
                var subjectEnd = normalized.IndexOf("\n\n", StringComparison.Ordinal);
                if (subjectEnd == -1)
                {
                    SetCurrentValue(SubjectProperty, normalized.ReplaceLineEndings(" "));
                    SetCurrentValue(DescriptionProperty, string.Empty);
                }
                else
                {
                    SetCurrentValue(SubjectProperty, normalized.Substring(0, subjectEnd).ReplaceLineEndings(" "));
                    SetCurrentValue(DescriptionProperty, normalized.Substring(subjectEnd + 2));
                }
                _changingWay = TextChangeWay.None;
            }
            else if ((change.Property == SubjectProperty || change.Property == DescriptionProperty) && _changingWay == TextChangeWay.None)
            {
                _changingWay = TextChangeWay.FromEditor;
                SetCurrentValue(TextProperty, $"{Subject}\n\n{Description}");
                _changingWay = TextChangeWay.None;
            }
        }

        private void OnSubjectTextBoxPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
                || (e.Key == Key.Right && SubjectEditor.CaretIndex == Subject.Length))
            {
                DescriptionEditor.Focus();
                DescriptionEditor.CaretIndex = 0;
                e.Handled = true;
            }
        }

        private void OnDescriptionTextBoxPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.Back || e.Key == Key.Left) && DescriptionEditor.CaretIndex == 0)
            {
                SubjectEditor.Focus();
                SubjectEditor.CaretIndex = Subject.Length;
                e.Handled = true;
            }
        }

        private TextChangeWay _changingWay = TextChangeWay.None;
    }
}
