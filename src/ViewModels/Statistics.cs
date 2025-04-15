﻿using System.Threading.Tasks;

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
            private set => SetProperty(ref _selectedReport, value);
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

        public Models.StaticsticsAuthor SelectedAuthor
        {
            get => _selectedAuthor;
            set
            {
                if (SetProperty(ref _selectedAuthor, value))
                {
                    _selectedReport?.ResetSelection();
                    
                    _selectedReport.SelectedAuthor = value;
                }
            }
        }

        public Statistics(string repo)
        {
            Task.Run(() =>
            {
                var result = new Commands.Statistics(repo, Preferences.Instance.MaxHistoryCommits).Result();
                Dispatcher.UIThread.Invoke(() =>
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

            SetProperty(ref _selectedAuthor, null);

            var report = _selectedIndex switch
            {
                0 => _data.All,
                1 => _data.Month,
                _ => _data.Week,
            };

            report.ResetSelection();
            report.ChangeColor(SampleColor);
            SelectedReport = report;
        }

        private bool _isLoading = true;
        private Models.Statistics _data = null;
        private Models.StatisticsReport _selectedReport = null;
        private int _selectedIndex = 0;
        private Models.StaticsticsAuthor _selectedAuthor = null;
    }
}
