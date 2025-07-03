using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class Merge : Popup
    {
        public object Source
        {
            get;
        }

        public string Into
        {
            get;
        }

        public Models.MergeMode Mode
        {
            get;
            set;
        }

        public bool Edit
        {
            get;
            set;
        } = false;

        public Merge(Repository repo, Models.Branch source, string into, bool forceFastForward)
        {
            _repo = repo;
            _sourceName = source.FriendlyName;

            Source = source;
            Into = into;
            Mode = forceFastForward ? Models.MergeMode.FastForward : AutoSelectMergeMode();
        }

        public Merge(Repository repo, Models.Commit source, string into)
        {
            _repo = repo;
            _sourceName = source.SHA;

            Source = source;
            Into = into;
            Mode = AutoSelectMergeMode();
        }

        public Merge(Repository repo, Models.Tag source, string into)
        {
            _repo = repo;
            _sourceName = source.Name;

            Source = source;
            Into = into;
            Mode = AutoSelectMergeMode();
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            _repo.ClearCommitMessage();
            ProgressDescription = $"Merging '{_sourceName}' into '{Into}' ...";

            var log = _repo.CreateLog($"Merging '{_sourceName}' into '{Into}'");
            Use(log);

            return Task.Run(() =>
            {
                new Commands.Merge(_repo.FullPath, _sourceName, Mode.Arg, Edit).Use(log).Exec();
                log.Complete();

                var head = new Commands.QueryRevisionByRefName(_repo.FullPath, "HEAD").Result();
                CallUIThread(() =>
                {
                    _repo.NavigateToCommit(head, true);
                    _repo.SetWatcherEnabled(true);
                });
                return true;
            });
        }

        private Models.MergeMode AutoSelectMergeMode()
        {
            return
                GetGitConfigBranchMergeOptions() // Branch
                ?? GetSettingsPreferredMergeMode() // Repository
                ?? GetGitConfigMergeFF(); // Global
        }

        private Models.MergeMode GetSettingsPreferredMergeMode()
        {
            var preferredMergeModeIdx = _repo.Settings.PreferredMergeMode;
            if (preferredMergeModeIdx < 0 || preferredMergeModeIdx > Models.MergeMode.Supported.Length)
                preferredMergeModeIdx = 0;

            var defaultMergeMode = Models.MergeMode.Supported[preferredMergeModeIdx];
            return defaultMergeMode;
        }

        private Models.MergeMode GetGitConfigMergeFF()
        {
            var config = new Commands.Config(_repo.FullPath).Get("merge.ff");
            return config switch
            {
                null or "" => null,
                "false" => Models.MergeMode.NoFastForward,
                "only" => Models.MergeMode.FastForward,
                _ => null
            };
        }

        private Models.MergeMode GetGitConfigBranchMergeOptions()
        {
            var config = new Commands.Config(_repo.FullPath).Get($"branch.{Into}.mergeoptions");
            return config switch
            {
                null or "" => null,
                "--ff-only" => Models.MergeMode.FastForward,
                "--no-ff" => Models.MergeMode.NoFastForward,
                "--squash" => Models.MergeMode.Squash,
                "--no-commit" or "--no-ff --no-commit" => Models.MergeMode.DontCommit,
                _ => null
            };
        }

        private readonly Repository _repo = null;
        private readonly string _sourceName;
    }
}
