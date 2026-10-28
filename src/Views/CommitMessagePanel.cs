using System;

using Avalonia;
using Avalonia.Controls;

namespace SourceGit.Views
{
    public class CommitMessagePanel : Panel
    {
        public static readonly StyledProperty<bool> RefsAfterSubjectProperty =
            AvaloniaProperty.Register<CommitMessagePanel, bool>(nameof(RefsAfterSubject));

        static CommitMessagePanel()
        {
            AffectsMeasure<CommitMessagePanel>(RefsAfterSubjectProperty);
        }

        public bool RefsAfterSubject
        {
            get => GetValue(RefsAfterSubjectProperty);
            set => SetValue(RefsAfterSubjectProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var refs = Children[0];
            var subject = Children[1];
            refs.Measure(availableSize);
            var gap = RefsAfterSubject && refs.DesiredSize.Width > 0 ? 4 : 0;
            subject.Measure(new Size(Math.Max(0, availableSize.Width - refs.DesiredSize.Width - gap), availableSize.Height));
            return new Size(refs.DesiredSize.Width + subject.DesiredSize.Width + gap,
                Math.Max(refs.DesiredSize.Height, subject.DesiredSize.Height));
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var refs = Children[0];
            var subject = Children[1];
            var refsWidth = Math.Min(refs.DesiredSize.Width, finalSize.Width);
            var gap = RefsAfterSubject && refsWidth > 0 ? 4 : 0;
            var subjectWidth = Math.Max(0, finalSize.Width - refsWidth - gap);
            if (RefsAfterSubject)
            {
                subjectWidth = Math.Min(subjectWidth, subject.DesiredSize.Width);
                subject.Arrange(new Rect(0, 0, subjectWidth, finalSize.Height));
                refs.Arrange(new Rect(subjectWidth + gap, 0, refsWidth, finalSize.Height));
            }
            else
            {
                refs.Arrange(new Rect(0, 0, refsWidth, finalSize.Height));
                subject.Arrange(new Rect(refsWidth, 0, subjectWidth, finalSize.Height));
            }

            return finalSize;
        }
    }
}
