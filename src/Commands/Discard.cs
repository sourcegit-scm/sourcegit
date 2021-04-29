using System;
using System.Collections.Generic;

namespace SourceGit.Commands {
    /// <summary>
    ///     忽略变更
    /// </summary>
    public class Discard {
        private string repo = null;
        private List<string> files = new List<string>();

        public Discard(string repo, List<Models.Change> changes) {
            this.repo = repo;
            
            if (changes != null && changes.Count > 0) {
                foreach (var c in changes) {
                    if (c.WorkTree == Models.Change.Status.Untracked || c.WorkTree == Models.Change.Status.Added) continue;
                    files.Add(c.Path);
                }
            }
        }

        public bool Exec() {
            if (files.Count == 0) {
                new Reset(repo, "HEAD", "--hard").Exec();
            } else {
                for (int i = 0; i < files.Count; i += 10) {
                    var count = Math.Min(10, files.Count - i);
                    new Checkout(repo).Files(files.GetRange(i, count));
                }
            }

            new Clean(repo).Exec();
            return true;
        }
    }
}
