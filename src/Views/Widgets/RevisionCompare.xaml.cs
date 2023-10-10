using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace SourceGit.Views.Widgets {
    /// <summary>
    ///     展示两个提交之间的变更
    /// </summary>
    public partial class RevisionCompare : UserControl {

        public RevisionCompare() {
            InitializeComponent();
        }

        public void SetData(string repo, Models.Commit start, Models.Commit end) {
            avatarStart.Email = start.Committer.Email;
            avatarStart.FallbackLabel = start.Committer.Name;
            avatarStart.ToolTip = start.Committer.Name;
            txtStartSHA.Text = start.ShortSHA;
            txtStartTime.Text = start.CommitterTimeStr;
            txtStartSubject.Text = start.Subject;

            avatarEnd.Email = end.Committer.Email;
            avatarEnd.FallbackLabel = end.Committer.Name;
            avatarEnd.ToolTip = end.Committer.Name;
            txtEndSHA.Text = end.ShortSHA;
            txtEndTime.Text = end.CommitterTimeStr;
            txtEndSubject.Text = end.Subject;

            Task.Run(() => {
                var changes = new Commands.CommitRangeChanges(repo, start.SHA, end.SHA).Result();
                Dispatcher.Invoke(() => {
                    changesContainer.SetData(repo, new List<Models.Commit>() { start, end }, changes);
                });
            });
        }
    }
}
