using System.Collections.Generic;
using System.Windows.Controls;

namespace SourceGit.Views.Widgets {
    /// <summary>
    ///     统计内容
    /// </summary>
    public partial class StatisticsPage : UserControl {

        public StatisticsPage() {
            InitializeComponent();
        }

        public void SetData(List<Models.StatisticSample> committers, List<Models.StatisticSample> commits, int totalCommits) {
            Dispatcher.Invoke(() => {
                txtMemberCount.Text = App.Text("Statistics.TotalCommitterCount", committers.Count);
                txtCommitCount.Text = App.Text("Statistics.TotalCommitsCount", totalCommits);

                lstCommitters.ItemsSource = committers;
                chartCommits.SetData(commits);
            });
        }
    }
}
