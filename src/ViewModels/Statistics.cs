using System.Threading.Tasks;
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

        public Models.StatisticsMode ViewMode
        {
            get => _viewMode;
            set
            {
                if (SetProperty(ref _viewMode, value))
                    RefreshReport();
            }
        }

        public Models.StatisticsReport SelectedReport
        {
            get => _selectedReport;
            private set => SetProperty(ref _selectedReport, value);
        }

        public Models.StatisticsSeries Series
        {
            get => _series;
            private set => SetProperty(ref _series, value);
        }

        public Statistics(string repo)
        {
            Task.Run(async () =>
            {
                var result = await new Commands.Statistics(repo, Preferences.Instance.MaxHistoryCommits)
                    .ReadAsync()
                    .ConfigureAwait(false);

                Dispatcher.UIThread.Post(() =>
                {
                    _data = result;
                    RefreshReport();
                    IsLoading = false;
                });
            });
        }

        public void ChangeAuthor(Models.StatisticsAuthor author)
        {
            if (SelectedReport == null)
                return;

            Series = SelectedReport.GetStatisticsSeries(author);
        }

        private void RefreshReport()
        {
            if (_data == null)
                return;

            SelectedReport = _viewMode switch
            {
                Models.StatisticsMode.All => _data.All,
                Models.StatisticsMode.ThisMonth => _data.Month,
                _ => _data.Week,
            };

            Series = SelectedReport.GetStatisticsSeries();
        }

        private bool _isLoading = true;
        private Models.Statistics _data = null;
        private Models.StatisticsMode _viewMode = Models.StatisticsMode.All;
        private Models.StatisticsReport _selectedReport = null;
        private Models.StatisticsSeries _series = null;
    }
}
