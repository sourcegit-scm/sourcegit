using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SourceGit.Views
{
    public partial class LinesChanged : UserControl
    {
        public static readonly DirectProperty<LinesChanged, int> AddedLinesProperty =
            AvaloniaProperty.RegisterDirect<LinesChanged, int>(nameof(AddedLines), o => o.AddedLines);

        public static readonly DirectProperty<LinesChanged, int> RemovedLinesProperty =
            AvaloniaProperty.RegisterDirect<LinesChanged, int>(nameof(RemovedLines), o => o.RemovedLines);

        private int _addedLines;
        private int _removedLines;

        public int AddedLines
        {
            get => _addedLines;
            set => SetAndRaise(AddedLinesProperty, ref _addedLines, value);
        }

        public int RemovedLines
        {
            get => _removedLines;
            set => SetAndRaise(RemovedLinesProperty, ref _removedLines, value);
        }

        public LinesChanged()
        {
            InitializeComponent();
            this.DataContext = this;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
