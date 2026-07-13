using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SourceGit.Views
{
    public class CommitStatusIndicator : Control
    {
        public static readonly DirectProperty<CommitStatusIndicator, Models.Branch> CurrentBranchProperty =
            AvaloniaProperty.RegisterDirect<CommitStatusIndicator, Models.Branch>(
                nameof(CurrentBranch),
                static o => o.CurrentBranch,
                static (o, v) => o.CurrentBranch = v);

        public Models.Branch CurrentBranch
        {
            get => _currentBranch;
            set => SetAndRaise(CurrentBranchProperty, ref _currentBranch, value);
        }

        public static readonly StyledProperty<IBrush> AheadBrushProperty =
            AvaloniaProperty.Register<CommitStatusIndicator, IBrush>(nameof(AheadBrush));

        public IBrush AheadBrush
        {
            get => GetValue(AheadBrushProperty);
            set => SetValue(AheadBrushProperty, value);
        }

        public static readonly StyledProperty<IBrush> BehindBrushProperty =
            AvaloniaProperty.Register<CommitStatusIndicator, IBrush>(nameof(BehindBrush));

        public IBrush BehindBrush
        {
            get => GetValue(BehindBrushProperty);
            set => SetValue(BehindBrushProperty, value);
        }

        private enum Status
        {
            Normal,
            Ahead,
            Behind,
        }

        public override void Render(DrawingContext context)
        {
            if (_status == Status.Normal)
                return;

            context.DrawEllipse(_status == Status.Ahead ? AheadBrush : BehindBrush, null, new Rect(0, 0, 5, 5));
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (DataContext is Models.Commit commit && _currentBranch != null)
            {
                var sha = commit.SHA;

                if (_currentBranch.Ahead.Contains(sha))
                    _status = Status.Ahead;
                else if (_currentBranch.Behind.Contains(sha))
                    _status = Status.Behind;
                else
                    _status = Status.Normal;
            }
            else
            {
                _status = Status.Normal;
            }

            return _status == Status.Normal ? new Size(0, 0) : new Size(9, 5);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            InvalidateMeasure();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == CurrentBranchProperty)
                InvalidateMeasure();
        }

        private Models.Branch _currentBranch = null;
        private Status _status = Status.Normal;
    }
}
