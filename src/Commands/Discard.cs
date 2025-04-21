using System;
using System.Collections.Generic;

namespace SourceGit.Commands
{
    public static class Discard
    {
        public static void All(string repo, bool includeIgnored, Models.ICommandLog log)
        {
            new Restore(repo) { Log = log }.Exec();
            new Clean(repo, includeIgnored) { Log = log }.Exec();
        }

        public static void Changes(string repo, List<Models.Change> changes, Models.ICommandLog log)
        {
            var needClean = new List<string>();
            var needCheckout = new List<string>();

            foreach (var c in changes)
            {
                if (c.WorkTree == Models.ChangeState.Untracked || c.WorkTree == Models.ChangeState.Added)
                    needClean.Add(c.Path);
                else
                    needCheckout.Add(c.Path);
            }

            for (int i = 0; i < needClean.Count; i += 10)
            {
                var count = Math.Min(10, needClean.Count - i);
                new Clean(repo, needClean.GetRange(i, count)) { Log = log }.Exec();
            }

            for (int i = 0; i < needCheckout.Count; i += 10)
            {
                var count = Math.Min(10, needCheckout.Count - i);
                new Restore(repo, needCheckout.GetRange(i, count), "--worktree --recurse-submodules") { Log = log }.Exec();
            }
        }
    }
}
