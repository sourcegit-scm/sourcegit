using System;
using System.Collections.Generic;

namespace SourceGit.Commands {
    /// <summary>
    ///     忽略变更
    /// </summary>
    public class Discard {
        private string repo = null;

        public Discard(string repo) {
            this.repo = repo;
        }

        public void Whole() {
            new Reset(repo, "HEAD", "--hard").Exec();
            new Clean(repo).Exec();
        }

        public void Changes(List<Models.Change> changes) {
            var needClean = new List<string>();
            var needCheckout = new List<string>();

            foreach (var c in changes) {
                if (c.WorkTree == Models.Change.Status.Untracked || c.WorkTree == Models.Change.Status.Added) {
                    needClean.Add(c.Path);
                } else {
                    needCheckout.Add(c.Path);
                }
            }

            for (int i = 0; i < needClean.Count; i += 10) {
                var count = Math.Min(10, needClean.Count - i);
                new Clean(repo, needClean.GetRange(i, count)).Exec();
            }

            for (int i = 0; i < needCheckout.Count; i += 10) {
                var count = Math.Min(10, needCheckout.Count - i);
                new Checkout(repo).Files(needCheckout.GetRange(i, count));
            }
        }
    }
}
