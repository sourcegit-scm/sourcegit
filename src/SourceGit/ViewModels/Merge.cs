using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class MergeMode
    {
        public string Name { get; set; }
        public string Desc { get; set; }
        public string Arg { get; set; }

        public MergeMode(string n, string d, string a)
        {
            Name = n;
            Desc = d;
            Arg = a;
        }
    }

    public class Merge : Popup
    {
        public string Source
        {
            get;
            private set;
        }

        public string Into
        {
            get;
            private set;
        }

        public List<MergeMode> Modes
        {
            get;
            private set;
        }

        public MergeMode SelectedMode
        {
            get;
            set;
        }

        public Merge(Repository repo, string source, string into)
        {
            _repo = repo;
            Source = source;
            Into = into;
            Modes = new List<MergeMode>() {
                new MergeMode("Default", "Fast-forward if possible", ""),
                new MergeMode("No Fast-forward", "Always create a merge commit", "--no-ff"),
                new MergeMode("Squash", "Use '--squash'", "--squash"),
                new MergeMode("Don't commit", "Merge without commit", "--no-commit"),
            };
            SelectedMode = Modes[0];
            View = new Views.Merge() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Merging '{Source}' into '{Into}' ...";

            return Task.Run(() =>
            {
                var succ = new Commands.Merge(_repo.FullPath, Source, SelectedMode.Arg, SetProgressDescription).Exec();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private readonly Repository _repo = null;
    }
}