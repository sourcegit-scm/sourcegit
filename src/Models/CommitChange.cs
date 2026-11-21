using System;
using System.Collections.Generic;

namespace SourceGit.Models
{
    /// <summary>
    /// A change with independent, immutable historical endpoints. An empty revision
    /// denotes an absent file, never the working copy.
    /// </summary>
    public class CommitChange : Change
    {
        public string BeforeRevision { get; set; } = string.Empty;
        public string AfterRevision { get; set; } = string.Empty;
        public string BeforePath { get; set; } = string.Empty;
        public string AfterPath { get; set; } = string.Empty;
        public string BeforeObject { get; set; } = string.Empty;
        public string AfterObject { get; set; } = string.Empty;
        public string BeforeMode { get; set; } = "000000";
        public string AfterMode { get; set; } = "000000";

        public void UpdateStatus()
        {
            Path = string.IsNullOrEmpty(AfterRevision) ? BeforePath : AfterPath;
            OriginalPath = string.Empty;
            if (string.IsNullOrEmpty(BeforeRevision))
                Index = ChangeState.Added;
            else if (string.IsNullOrEmpty(AfterRevision))
                Index = ChangeState.Deleted;
            else if (!BeforePath.Equals(AfterPath, StringComparison.Ordinal))
            {
                Index = ChangeState.Renamed;
                OriginalPath = BeforePath;
            }
            else if (BeforeMode[..3] != AfterMode[..3])
                Index = ChangeState.TypeChanged;
            else
                Index = ChangeState.Modified;
        }

        /// <summary>
        /// Join changes oldest first, following paths, like the log's zipped changes
        /// in JetBrains clients. This is not a cherry-pick or a whole-range diff:
        /// only selected changes introduce paths, but joined endpoints may include
        /// intervening edits to those paths. Deletion followed by addition remains
        /// two changes, while addition followed by deletion cancels out.
        /// </summary>
        public static List<CommitChange> Zip(IEnumerable<CommitChange> changes)
        {
            var result = new List<CommitChange>();
            var byAfterPath = new Dictionary<string, Queue<CommitChange>>(StringComparer.Ordinal);
            foreach (var change in changes)
            {
                var combined = change;
                if (!string.IsNullOrEmpty(change.BeforeRevision) &&
                    byAfterPath.TryGetValue(change.BeforePath, out var previous) && previous.Count > 0)
                {
                    var first = previous.Dequeue();
                    result.Remove(first);
                    if (string.IsNullOrEmpty(first.BeforeRevision) && string.IsNullOrEmpty(change.AfterRevision))
                        continue;

                    combined = new CommitChange
                    {
                        BeforeRevision = first.BeforeRevision,
                        BeforePath = first.BeforePath,
                        BeforeObject = first.BeforeObject,
                        BeforeMode = first.BeforeMode,
                        AfterRevision = change.AfterRevision,
                        AfterPath = change.AfterPath,
                        AfterObject = change.AfterObject,
                        AfterMode = change.AfterMode,
                    };
                    combined.UpdateStatus();
                }

                result.Add(combined);
                if (!string.IsNullOrEmpty(combined.AfterRevision))
                {
                    if (!byAfterPath.TryGetValue(combined.AfterPath, out var bucket))
                    {
                        bucket = new Queue<CommitChange>();
                        byAfterPath.Add(combined.AfterPath, bucket);
                    }
                    bucket.Enqueue(combined);
                }
            }

            return result;
        }
    }
}
