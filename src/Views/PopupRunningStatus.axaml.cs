using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class PopupRunningStatus : UserControl
    {
        public static readonly DirectProperty<PopupRunningStatus, string> DescriptionProperty =
            AvaloniaProperty.RegisterDirect<PopupRunningStatus, string>(
                nameof(Description),
                static o => o.Description,
                static (o, v) => o.Description = v);

        public string Description
        {
            get => _description;
            set => SetAndRaise(DescriptionProperty, ref _description, value);
        }

        public PopupRunningStatus()
        {
            InitializeComponent();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            _isUnloading = false;
            if (IsVisible)
                StartAnim();
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            _isUnloading = true;
            base.OnUnloaded(e);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == IsVisibleProperty)
            {
                if (IsVisible && !_isUnloading)
                    StartAnim();
                else
                    StopAnim();
            }
            else if (change.Property == DescriptionProperty)
            {
                if (IsVisible && !_isUnloading)
                    UpdateProgress();
            }
        }

        private void StartAnim()
        {
            Icon.Content = new Path() { Classes = { "waiting" } };
            UpdateProgress();
        }

        private void StopAnim()
        {
            if (Icon.Content is Path path)
                path.Classes.Clear();
            Icon.Content = null;
            ProgressBar.IsIndeterminate = false;
            ProgressBar.Value = 0;
            Percentage.IsVisible = false;
        }

        private void UpdateProgress()
        {
            // Git reports the progress of long-running operations as `<phase>: NN% (...)`. Only the
            // percentage is used here, so it also works with translated phase names.
            var match = string.IsNullOrEmpty(_description) ? null : REG_PROGRESS().Match(_description);
            if (match is { Success: true } && int.TryParse(match.Groups[1].Value, out var percent) && percent <= 100)
            {
                ProgressBar.IsIndeterminate = false;
                ProgressBar.Value = percent;
                Percentage.Text = $"{percent}%";
                Percentage.IsVisible = true;
            }
            else
            {
                ProgressBar.IsIndeterminate = true;
                Percentage.IsVisible = false;
            }
        }

        private string _description = string.Empty;
        private bool _isUnloading = false;

        [GeneratedRegex(@"^[^%]+?:\s+(\d{1,3})%")]
        private static partial Regex REG_PROGRESS();
    }
}
