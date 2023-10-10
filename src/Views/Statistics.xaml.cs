using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SourceGit.Views {
    /// <summary>
    ///     提交统计
    /// </summary>
    public partial class Statistics : Controls.Window {
        private string repo = null;

        public Statistics(string repo) {
            this.repo = repo;
            InitializeComponent();
            Task.Run(Refresh);
        }

        private void Quit(object sender, RoutedEventArgs e) {
            Close();
        }

        private void Refresh() {
            var mapsWeek = new Dictionary<int, Models.StatisticSample>();
            for (int i = 0; i < 7; i++) {
                mapsWeek.Add(i, new Models.StatisticSample {
                    Name = App.Text($"Weekday.{i}"),
                    Index = i,
                    Count = 0,
                });
            }

            var mapsMonth = new Dictionary<int, Models.StatisticSample>();
            var today = DateTime.Now;
            var maxDays = DateTime.DaysInMonth(today.Year, today.Month);
            for (int i = 1; i <= maxDays; i++) {
                mapsMonth.Add(i, new Models.StatisticSample {
                    Name = $"{i}",
                    Index = i,
                    Count = 0,
                });
            }

            var mapsYear = new Dictionary<int, Models.StatisticSample>();
            for (int i = 1; i <= 12; i++) {
                mapsYear.Add(i, new Models.StatisticSample {
                    Name = App.Text($"Month.{i}"),
                    Index = i,
                    Count = 0,
                });
            }

            var mapCommitterWeek = new Dictionary<string, Models.StatisticSample>();
            var mapCommitterMonth = new Dictionary<string, Models.StatisticSample>();
            var mapCommitterYear = new Dictionary<string, Models.StatisticSample>();

            var weekStart = today.AddSeconds(-(int)today.DayOfWeek * 3600 * 24 - today.Hour * 3600 - today.Minute * 60 - today.Second);
            var weekEnd = weekStart.AddDays(7);
            var month = today.Month;
            var utcStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToLocalTime();

            var limits = $"--branches --remotes --since=\"{today.ToString("yyyy-01-01 00:00:00")}\"";
            var commits = new Commands.Commits(repo, limits).Result();
            var totalCommitsWeek = 0;
            var totalCommitsMonth = 0;
            var totalCommitsYear = commits.Count;
            foreach (var c in commits) {
                var commitTime = utcStart.AddSeconds(c.CommitterTime);
                if (commitTime.CompareTo(weekStart) >= 0 && commitTime.CompareTo(weekEnd) < 0) {
                    mapsWeek[(int)commitTime.DayOfWeek].Count++;
                    totalCommitsWeek++;

                    if (mapCommitterWeek.ContainsKey(c.Committer.Name)) {
                        mapCommitterWeek[c.Committer.Name].Count++;
                    } else {
                        mapCommitterWeek[c.Committer.Name] = new Models.StatisticSample {
                            Name = c.Committer.Name,
                            Count = 1,
                        };
                    }
                }

                if (commitTime.Month == month) {
                    mapsMonth[commitTime.Day].Count++;
                    totalCommitsMonth++;

                    if (mapCommitterMonth.ContainsKey(c.Committer.Name)) {
                        mapCommitterMonth[c.Committer.Name].Count++;
                    } else {
                        mapCommitterMonth[c.Committer.Name] = new Models.StatisticSample {
                            Name = c.Committer.Name,
                            Count = 1,
                        };
                    }
                }

                mapsYear[commitTime.Month].Count++;
                if (mapCommitterYear.ContainsKey(c.Committer.Name)) {
                    mapCommitterYear[c.Committer.Name].Count++;
                } else {
                    mapCommitterYear[c.Committer.Name] = new Models.StatisticSample {
                        Name = c.Committer.Name,
                        Count = 1,
                    };
                }
            }

            SetPage(pageWeek, mapCommitterWeek.Values.ToList(), mapsWeek.Values.ToList(), totalCommitsWeek);
            SetPage(pageMonth, mapCommitterMonth.Values.ToList(), mapsMonth.Values.ToList(), totalCommitsMonth);
            SetPage(pageYear, mapCommitterYear.Values.ToList(), mapsYear.Values.ToList(), totalCommitsYear);

            mapsMonth.Clear();
            mapsWeek.Clear();
            mapsYear.Clear();
            mapCommitterMonth.Clear();
            mapCommitterWeek.Clear();
            mapCommitterYear.Clear();
            commits.Clear();

            Dispatcher.Invoke(() => {
                loading.IsAnimating = false;
                loading.Visibility = Visibility.Collapsed;
            });
        }

        private void SetPage(Widgets.StatisticsPage page, List<Models.StatisticSample> committers, List<Models.StatisticSample> commits, int total) {
            committers.Sort((x, y) => y.Count - x.Count);
            commits.Sort((x, y) => x.Index - y.Index);
            page.SetData(committers, commits, total);
        }
    }
}
