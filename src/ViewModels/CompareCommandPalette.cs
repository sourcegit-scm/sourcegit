using System;
using System.Collections.Generic;

namespace SourceGit.ViewModels
{
    public class CompareCommandPalette : ICommandPalette
    {
        public object BasedOn
        {
            get => _basedOn;
        }

        public object CompareTo
        {
            get => _compareTo;
            set => SetProperty(ref _compareTo, value);
        }

        public List<object> Refs
        {
            get => _refs;
            private set => SetProperty(ref _refs, value);
        }

        public string Filter
        {
            get => _filter;
            set
            {
                if (SetProperty(ref _filter, value))
                    UpdateRefs();
            }
        }

        public CompareCommandPalette(Repository repo, object basedOn)
        {
            _repo = repo;
            _basedOn = basedOn ?? repo.CurrentBranch;
            UpdateRefs();
        }

        public void ClearFilter()
        {
            Filter = string.Empty;
        }

        public Compare Launch()
        {
            _refs.Clear();
            Close();
            return _compareTo != null ? new Compare(_repo, _basedOn, _compareTo) : null;
        }

        private void UpdateRefs()
        {
            var refs = new List<object>();

            foreach (var b in _repo.Branches)
            {
                if (b == _basedOn)
                    continue;

                if (string.IsNullOrEmpty(_filter) || b.FriendlyName.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                    refs.Add(b);
            }

            foreach (var t in _repo.Tags)
            {
                if (t == _basedOn)
                    continue;

                if (string.IsNullOrEmpty(_filter) || t.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                    refs.Add(t);
            }

            refs.Sort((l, r) =>
            {
                if (l is Models.Branch lb)
                {
                    if (r is Models.Branch rb)
                    {
                        if (lb.IsLocal == rb.IsLocal)
                            return Models.NumericSort.Compare(lb.FriendlyName, rb.FriendlyName);
                        return lb.IsLocal ? -1 : 1;
                    }

                    return -1;
                }

                if (r is Models.Branch)
                    return 1;

                return Models.NumericSort.Compare((l as Models.Tag).Name, (r as Models.Tag).Name);
            });

            var autoSelected = _compareTo;
            if (refs.Count == 0)
                autoSelected = null;
            else if (_compareTo == null || !refs.Contains(_compareTo))
                autoSelected = refs[0];

            Refs = refs;
            CompareTo = autoSelected;
        }

        private Repository _repo;
        private object _basedOn = null;
        private object _compareTo = null;
        private List<object> _refs = [];
        private string _filter;
    }
}
