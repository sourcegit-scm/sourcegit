using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
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

        public Models.MergeMode SelectedMode
        {
            get;
            set;
        }

        public Merge(Repository repo, string source, string into)
        {
            _repo = repo;
            Source = source;
            Into = into;
            SelectedMode = Models.MergeMode.Supported[0];
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
