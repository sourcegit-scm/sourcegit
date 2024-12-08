using System;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class Merge : Popup
    {
        public string Source
        {
            get;
        }

        public string Into
        {
            get;
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
            SelectedMode = AutoSelectMergeMode();
            View = new Views.Merge() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Merging '{Source}' into '{Into}' ...";

            return Task.Run(() =>
            {
                var succ = new Commands.Merge(_repo.FullPath, Source, SelectedMode.Arg, null, SetProgressDescription).Exec();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private Models.MergeMode AutoSelectMergeMode()
        {
            var config = new Commands.Config(_repo.FullPath).Get($"branch.{Into}.mergeoptions");
            if (string.IsNullOrEmpty(config))
                return Models.MergeMode.Supported[0];
            if (config.Equals("--no-ff", StringComparison.Ordinal))
                return Models.MergeMode.Supported[1];
            if (config.Equals("--squash", StringComparison.Ordinal))
                return Models.MergeMode.Supported[2];
            if (config.Equals("--no-commit", StringComparison.Ordinal) || config.Equals("--no-ff --no-commit", StringComparison.Ordinal))
                return Models.MergeMode.Supported[3];

            return Models.MergeMode.Supported[0];
        }

        private readonly Repository _repo = null;
    }
}
