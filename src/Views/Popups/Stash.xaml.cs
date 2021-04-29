using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.Views.Popups {

    /// <summary>
    ///     贮藏
    /// </summary>
    public partial class Stash : Controls.PopupWidget {
        private string repo = null;
        private List<string> files = null;

        public Stash(string repo, List<string> files) {
            this.repo = repo;
            this.files = files;

            InitializeComponent();
            chkIncludeUntracked.IsEnabled = files == null || files.Count == 0;
        }

        public override string GetTitle() {
            return App.Text("Stash.Title");
        }

        public override Task<bool> Start() {
            var includeUntracked = chkIncludeUntracked.IsChecked == true;
            var message = txtMessage.Text;

            return Task.Run(() => {
                Models.Watcher.SetEnabled(repo, false);
                if (files == null || files.Count == 0) {
                    new Commands.Stash(repo).Push(null, message, includeUntracked);
                } else {
                    for (int i = 0; i < files.Count; i += 10) {
                        var count = Math.Min(10, files.Count - i);
                        var step = files.GetRange(i, count);
                        new Commands.Stash(repo).Push(step, message, includeUntracked);
                    }
                }
                Models.Watcher.SetEnabled(repo, true);
                return true;
            });
        }
    }
}
