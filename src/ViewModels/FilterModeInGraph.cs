﻿using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class FilterModeInGraph : ObservableObject
    {
        public bool IsFiltered
        {
            get => _mode == Models.FilterMode.Included;
            set => SetFilterMode(value ? Models.FilterMode.Included : Models.FilterMode.None);
        }

        public bool IsExcluded
        {
            get => _mode == Models.FilterMode.Excluded;
            set => SetFilterMode(value ? Models.FilterMode.Excluded : Models.FilterMode.None);
        }

        public FilterModeInGraph(Repository repo, object target)
        {
            _repo = repo;
            _target = target;

            if (_target is Models.Branch b)
                _mode = GetFilterMode(b.FullName);
            else if (_target is Models.Tag t)
                _mode = GetFilterMode(t.Name);
        }

        private Models.FilterMode GetFilterMode(string pattern)
        {
            foreach (var filter in _repo.Settings.HistoriesFilters)
            {
                if (filter.Pattern.Equals(pattern, StringComparison.Ordinal))
                    return filter.Mode;
            }

            return Models.FilterMode.None;
        }

        private void SetFilterMode(Models.FilterMode mode)
        {
            if (_mode != mode)
            {
                _mode = mode;

                if (_target is Models.Branch branch)
                    _repo.SetBranchFilterMode(branch, _mode, false, true);
                else if (_target is Models.Tag tag)
                    _repo.SetTagFilterMode(tag, _mode);

                OnPropertyChanged(nameof(IsFiltered));
                OnPropertyChanged(nameof(IsExcluded));
            }
        }

        private readonly Repository _repo;
        private readonly object _target;
        private Models.FilterMode _mode = Models.FilterMode.None;
    }
}
