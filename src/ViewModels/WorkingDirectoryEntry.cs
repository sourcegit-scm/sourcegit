using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    // Presentation-only history entry. It has no revision identity and never enters commit queries.
    public class WorkingDirectoryEntry : ObservableObject
    {
        public string Summary => App.Text("Histories.WorkingDirectory");
        public double LeftMargin { get => _leftMargin; set => SetProperty(ref _leftMargin, value); }
        public int Modified { get => _modified; private set => SetProperty(ref _modified, value); }
        public int Added { get => _added; private set => SetProperty(ref _added, value); }
        public int Deleted { get => _deleted; private set => SetProperty(ref _deleted, value); }
        public bool HasChanges => Modified + Added + Deleted > 0;

        public void SetChanges(List<Models.Change> changes)
        {
            var modified = 0;
            var added = 0;
            var deleted = 0;
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var c in changes)
            {
                if (!paths.Add(c.Path))
                    continue;

                // Structural changes and conflicts take precedence over addition/deletion.
                // AM/AD remain additions: the index introduces a path absent from HEAD.
                // MD is deleted; RD stays modified (the rename still exists in the index).
                if (IsStructural(c.Index) || IsStructural(c.WorkTree))
                    modified++;
                else if (c.Index == Models.ChangeState.Added || c.WorkTree is Models.ChangeState.Added or Models.ChangeState.Untracked)
                    added++;
                else if (c.Index == Models.ChangeState.Deleted || c.WorkTree == Models.ChangeState.Deleted)
                    deleted++;
                else
                    modified++;
            }
            Modified = modified;
            Added = added;
            Deleted = deleted;
        }

        private static bool IsStructural(Models.ChangeState state) => state is
            Models.ChangeState.Renamed or Models.ChangeState.Copied or Models.ChangeState.TypeChanged or Models.ChangeState.Conflicted;

        private double _leftMargin = 24;
        private int _modified;
        private int _added;
        private int _deleted;
    }
}
