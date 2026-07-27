using System.Collections.Generic;
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

        public List<Models.Branch> Branches
        {
            get => _branches;
            private set => SetProperty(ref _branches, value);
        }

        public Models.Branch SelectedBranch
        {
            get => _selectedBranch;
            set
            {
                if (SetProperty(ref _selectedBranch, value))
                    LoadStatistics();
            }
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

        public Models.StatisticsSamples Samples
        {
            get => _samples;
            private set => SetProperty(ref _samples, value);
        }

        public Statistics(string repo)
        {
            _repo = repo;
            LoadBranches();
            LoadStatistics();
        }

        public void ChangeAuthor(Models.StatisticsAuthor author)
        {
            if (SelectedReport == null)
                return;

            Samples = SelectedReport.GetSamples(author);
        }

        private void LoadBranches()
        {
            Task.Run(async () =>
            {
                var branches = await new Commands.QueryBranches(_repo)
                    .GetResultAsync()
                    .ConfigureAwait(false);

                branches.Insert(0, _selectedBranch);
                Dispatcher.UIThread.Post(() => Branches = branches);
            });
        }

        private void LoadStatistics()
        {
            IsLoading = true;

            Task.Run(async () =>
            {
                var result = await new Commands.Statistics(_repo, Preferences.Instance.MaxHistoryCommits, _selectedBranch)
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

            Samples = SelectedReport.GetSamples(null);
        }

        private string _repo = null;
        private bool _isLoading = true;
        private List<Models.Branch> _branches = new List<Models.Branch>();
        private Models.Branch _selectedBranch = new Models.Branch() { Name = "--- (All)", IsLocal = true, FullName = "", Head = "---" }; // Fake branch to represent all branches
        private Models.Statistics _data = null;
        private Models.StatisticsMode _viewMode = Models.StatisticsMode.All;
        private Models.StatisticsReport _selectedReport = null;
        private Models.StatisticsSamples _samples = null;
    }
}
