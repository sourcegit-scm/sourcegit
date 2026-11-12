using Avalonia;
using Avalonia.Controls;

namespace SourceGit.Views
{
    public class ChangeListLayout : Grid
    {
        public static readonly StyledProperty<bool> IsFileListOnlyProperty =
            AvaloniaProperty.Register<ChangeListLayout, bool>(nameof(IsFileListOnly));

        public static readonly StyledProperty<GridLength> ListWidthProperty =
            AvaloniaProperty.Register<ChangeListLayout, GridLength>(nameof(ListWidth), new GridLength(256));

        public bool IsFileListOnly
        {
            get => GetValue(IsFileListOnlyProperty);
            set => SetValue(IsFileListOnlyProperty, value);
        }

        public GridLength ListWidth
        {
            get => GetValue(ListWidthProperty);
            set => SetValue(ListWidthProperty, value);
        }

        protected override System.Type StyleKeyOverride => typeof(Grid);

        public ChangeListLayout()
        {
            ColumnDefinitions = new ColumnDefinitions("256,4,*");
            ColumnDefinitions[0].PropertyChanged += (_, e) =>
            {
                if (!_updating && !IsFileListOnly && e.Property == ColumnDefinition.WidthProperty)
                    SetCurrentValue(ListWidthProperty, ColumnDefinitions[0].Width);
            };
            RefreshLayout();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == IsFileListOnlyProperty || change.Property == ListWidthProperty)
                RefreshLayout();
        }

        private void RefreshLayout()
        {
            _updating = true;
            ColumnDefinitions[0].MinWidth = IsFileListOnly ? 0 : 200;
            ColumnDefinitions[0].Width = IsFileListOnly ? GridLength.Star : ListWidth;
            ColumnDefinitions[1].Width = new GridLength(IsFileListOnly ? 0 : 4);
            ColumnDefinitions[2].MinWidth = IsFileListOnly ? 0 : 100;
            ColumnDefinitions[2].Width = IsFileListOnly ? new GridLength(0) : GridLength.Star;
            _updating = false;
        }

        private bool _updating = false;
    }
}
