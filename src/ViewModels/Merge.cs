﻿using System;
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

        public Merge(Repository repo, Models.Branch source, string into, bool forceFastForward)
        {
            _repo = repo;
            _sourceName = source.FriendlyName;

            Source = source;
            Into = into;
            Mode = forceFastForward ? Models.MergeMode.Supported[1] : AutoSelectMergeMode();
            View = new Views.Merge() { DataContext = this };
        }

        public Merge(Repository repo, Models.Commit source, string into)
        {
            _repo = repo;
            _sourceName = source.SHA;

            Source = source;
            Into = into;
            Mode = AutoSelectMergeMode();
            View = new Views.Merge() { DataContext = this };
        }

        public Merge(Repository repo, Models.Tag source, string into)
        {
            _repo = repo;
            _sourceName = source.Name;

            Source = source;
            Into = into;
            Mode = AutoSelectMergeMode();
            View = new Views.Merge() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Merging '{_sourceName}' into '{Into}' ...";

            return Task.Run(() =>
            {
                new Commands.Merge(_repo.FullPath, _sourceName, Mode.Arg, SetProgressDescription).Exec();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return true;
            });
        }

        private Models.MergeMode AutoSelectMergeMode()
        {
            var preferredMergeModeIdx = _repo.Settings.PreferredMergeMode;
            if (preferredMergeModeIdx < 0 || preferredMergeModeIdx > Models.MergeMode.Supported.Length)
                preferredMergeModeIdx = 0;

            var defaultMergeMode = Models.MergeMode.Supported[preferredMergeModeIdx];
            var config = new Commands.Config(_repo.FullPath).Get($"branch.{Into}.mergeoptions");
            if (string.IsNullOrEmpty(config))
                return defaultMergeMode;
            if (config.Equals("--ff-only", StringComparison.Ordinal))
                return Models.MergeMode.Supported[1];
            if (config.Equals("--no-ff", StringComparison.Ordinal))
                return Models.MergeMode.Supported[2];
            if (config.Equals("--squash", StringComparison.Ordinal))
                return Models.MergeMode.Supported[3];
            if (config.Equals("--no-commit", StringComparison.Ordinal) || config.Equals("--no-ff --no-commit", StringComparison.Ordinal))
                return Models.MergeMode.Supported[4];

            return defaultMergeMode;
        }

        private readonly Repository _repo = null;
        private readonly string _sourceName = string.Empty;
    }
}
