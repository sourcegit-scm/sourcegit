using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace SourceGit.Views {
    /// <summary>
    ///     提交统计
    /// </summary>
    public partial class Statistics : Controls.Window {
        private static readonly string[] WEEK_DAYS = new string[] { "一", "二", "三", "四", "五", "六", "日" };
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
                    Name = $"星期{WEEK_DAYS[i]}",
                    Count = 0,
                });
            }

            var mapsMonth = new Dictionary<int, Models.StatisticSample>();
            var today = DateTime.Now;
            var maxDays = DateTime.DaysInMonth(today.Year, today.Month);
            for (int i = 1; i <= maxDays; i++) {
                mapsMonth.Add(i, new Models.StatisticSample {
                    Name = $"{i}",
                    Count = 0,
                });
            }

            var mapCommitterWeek = new Dictionary<string, Models.StatisticSample>();
            var mapCommitterMonth = new Dictionary<string, Models.StatisticSample>();
            var week = today.DayOfWeek;
            var month = today.Month;

            var limits = $"--since=\"{today.ToString("yyyy-MM-01 00:00:00")}\"";
            var commits = new Commands.Commits(repo, limits).Result();
            var totalCommitsMonth = commits.Count;
            var totalCommitsWeek = 0;
            foreach (var c in commits) {
                var commitTime = DateTime.Parse(c.Committer.Time);
                if (IsSameWeek(today, commitTime)) {
                    mapsWeek[(int)commitTime.DayOfWeek].Count++;
                    if (mapCommitterWeek.ContainsKey(c.Committer.Name)) {
                        mapCommitterWeek[c.Committer.Name].Count++;
                    } else {
                        mapCommitterWeek[c.Committer.Name] = new Models.StatisticSample {
                            Name = c.Committer.Name,
                            Count = 1,
                        };
                    }
                    
                    totalCommitsWeek++;
                }

                mapsMonth[commitTime.Day].Count++;

                if (mapCommitterMonth.ContainsKey(c.Committer.Name)) {
                    mapCommitterMonth[c.Committer.Name].Count++;
                } else {
                    mapCommitterMonth[c.Committer.Name] = new Models.StatisticSample {
                        Name = c.Committer.Name,
                        Count = 1,
                    };
                }
            }

            var samplesChartWeek = new List<Models.StatisticSample>();
            var samplesChartMonth = new List<Models.StatisticSample>();
            var samplesCommittersWeek = new List<Models.StatisticSample>();
            var samplesCommittersMonth = new List<Models.StatisticSample>();
            for (int i = 0; i < 7; i++) samplesChartWeek.Add(mapsWeek[i]);
            for (int i = 1; i <= maxDays; i++) samplesChartMonth.Add(mapsMonth[i]);
            foreach (var kv in mapCommitterWeek) samplesCommittersWeek.Add(kv.Value);
            foreach (var kv in mapCommitterMonth) samplesCommittersMonth.Add(kv.Value);
            mapsMonth.Clear();
            mapsWeek.Clear();
            mapCommitterMonth.Clear();
            mapCommitterWeek.Clear();
            commits.Clear();
            samplesCommittersWeek.Sort((x, y) => y.Count - x.Count);
            samplesCommittersMonth.Sort((x, y) => y.Count - x.Count);

            Dispatcher.Invoke(() => {
                loading.IsAnimating = false;
                loading.Visibility = Visibility.Collapsed;

                chartWeek.SetData(samplesChartWeek);
                chartMonth.SetData(samplesChartMonth);

                lstCommitterWeek.ItemsSource = samplesCommittersWeek;
                lstCommitterMonth.ItemsSource = samplesCommittersMonth;

                txtMemberCountWeek.Text = App.Text("Statistics.TotalCommitterCount", samplesCommittersWeek.Count);
                txtMemberCountMonth.Text = App.Text("Statistics.TotalCommitterCount", samplesCommittersMonth.Count);
                txtCommitCountWeek.Text = App.Text("Statistics.TotalCommitsCount", totalCommitsWeek);
                txtCommitCountMonth.Text = App.Text("Statistics.TotalCommitsCount", totalCommitsMonth);
            });
        }

        private bool IsSameWeek(DateTime t1, DateTime t2) {
            double diffDay = t1.Subtract(t2).Duration().TotalDays;
            if (diffDay >= 7) return false;

            return t1.CompareTo(t2) > 0 ? (t1.DayOfWeek >= t2.DayOfWeek) : t1.DayOfWeek <= t2.DayOfWeek;
        }
    }
}
