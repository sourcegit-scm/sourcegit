using Avalonia;
using Avalonia.Controls;

namespace SourceGit.Views
{
    public partial class LinesChanged : UserControl
    {
        public static readonly StyledProperty<int> AddedCountProperty =
            AvaloniaProperty.Register<LinesChanged, int>(nameof(AddedCount));

        public static readonly StyledProperty<int> RemovedCountProperty =
            AvaloniaProperty.Register<LinesChanged, int>(nameof(RemovedCount));

        public int AddedCount
        {
            get => GetValue(AddedCountProperty);
            set => SetValue(AddedCountProperty, value);
        }

        public int RemovedCount
        {
            get => GetValue(RemovedCountProperty);
            set => SetValue(RemovedCountProperty, value);
        }

        public LinesChanged()
        {
            InitializeComponent();
        }
    }
}
