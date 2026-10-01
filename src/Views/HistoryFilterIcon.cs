using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace SourceGit.Views
{
    public class HistoryFilterIcon : ContentControl
    {
        public static readonly DirectProperty<HistoryFilterIcon, Models.HistoryFilter> FilterProperty =
            AvaloniaProperty.RegisterDirect<HistoryFilterIcon, Models.HistoryFilter>(
                nameof(Filter),
                static o => o.Filter,
                static (o, v) => o.Filter = v);

        public Models.HistoryFilter Filter
        {
            get => _filter;
            set => SetAndRaise(FilterProperty, ref _filter, value);
        }

        public static readonly DirectProperty<HistoryFilterIcon, bool> IsFilterValidProperty =
            AvaloniaProperty.RegisterDirect<HistoryFilterIcon, bool>(
                nameof(IsFilterValid),
                static o => o.IsFilterValid,
                static (o, v) => o.IsFilterValid = v);

        public bool IsFilterValid
        {
            get => _isFilterValid;
            set => SetAndRaise(IsFilterValidProperty, ref _isFilterValid, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == FilterProperty || change.Property == IsFilterValidProperty)
                UpdateContent();
        }

        private void UpdateContent()
        {
            if (_filter == null)
            {
                Content = null;
                return;
            }

            if (_filter.Type is Models.FilterType.LocalBranch or Models.FilterType.RemoteBranch)
            {
                Padding = new Thickness(0);

                if (_isFilterValid)
                    CreateContent("Icons.Branch");
                else
                    CreateContent("Icons.Error", Brushes.DarkOrange);
            }
            else if (_filter.Type is Models.FilterType.Tag)
            {
                Padding = new Thickness(0);

                if (_isFilterValid)
                    CreateContent("Icons.Tag");
                else
                    CreateContent("Icons.Error", Brushes.DarkOrange);
            }
            else
            {
                Padding = new Thickness(0, 1, 0, 0);
                CreateContent("Icons.Folder");
            }
        }

        private void CreateContent(string iconKey, IBrush fill = null)
        {
            if (this.FindResource(iconKey) is not StreamGeometry geo)
                return;

            var path = new Path()
            {
                Width = 10,
                Height = 10,
                Data = geo,
            };

            if (fill != null)
                path.Fill = fill;

            Content = path;
        }

        private Models.HistoryFilter _filter = null;
        private bool _isFilterValid = true;
    }
}
