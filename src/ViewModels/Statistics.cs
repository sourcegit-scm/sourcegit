using System.Threading.Tasks;

using Avalonia.Media;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class Statistics : ObservableObject
    {
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (SetProperty(ref _selectedIndex, value))
                    RefreshReport();
            }
        }

        public Models.StatisticsReport SelectedReport
        {
            get => _selectedReport;
            private set
            {
                value?.ChangeAuthor(null);
                SetProperty(ref _selectedReport, value);
            }
        }

        public uint SampleColor
        {
            get => Preferences.Instance.StatisticsSampleColor;
            set
            {
                if (value != Preferences.Instance.StatisticsSampleColor)
                {
                    Preferences.Instance.StatisticsSampleColor = value;
                    OnPropertyChanged(nameof(SampleBrush));
                    _selectedReport?.ChangeColor(value);
                }
            }
        }

        public IBrush SampleBrush
        {
            get => new SolidColorBrush(SampleColor);
        }

        public Statistics(string repo)
        {
            Task.Run(async () =>
            {
                var result = await new Commands.Statistics(repo, Preferences.Instance.MaxHistoryCommits).ReadAsync().ConfigureAwait(false);
                Dispatcher.UIThread.Post(() =>
                {
                    _data = result;
                    RefreshReport();
                    IsLoading = false;
                });
            });
        }

        private void RefreshReport()
        {
            if (_data == null)
                return;

            var report = _selectedIndex switch
            {
                0 => _data.All,
                1 => _data.Month,
                _ => _data.Week,
            };

            report.ChangeColor(SampleColor);
            SelectedReport = report;
        }

        private bool _isLoading = true;
        private Models.Statistics _data = null;
        private Models.StatisticsReport _selectedReport = null;
        private int _selectedIndex = 0;
    }
}
