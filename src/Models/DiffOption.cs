using System;
using System.Collections.Generic;
using System.Text;

namespace SourceGit.Models
{
    public class DiffOption
    {
        /// <summary>
        ///     Enable `--ignore-cr-at-eol` by default?
        /// </summary>
        public static bool IgnoreCRAtEOL
        {
            get;
            set;
        } = true;

        public Change WorkingCopyChange => _workingCopyChange;
        public bool IsUnstaged => _isUnstaged;
        public List<string> Revisions => _revisions;
        public string Path => _path;
        public string OrgPath => _orgPath;

        /// <summary>
        ///     Only used for working copy changes
        /// </summary>
        /// <param name="change"></param>
        /// <param name="isUnstaged"></param>
        public DiffOption(Change change, bool isUnstaged)
        {
            _workingCopyChange = change;
            _isUnstaged = isUnstaged;
            _path = change.Path;
            _orgPath = change.OriginalPath;

            if (isUnstaged)
            {
                switch (change.WorkTree)
                {
                    case ChangeState.Added:
                    case ChangeState.Untracked:
                        _extra = "--no-index";
                        _orgPath = "/dev/null";
                        break;
                }
            }
            else
            {
                if (change.DataForAmend != null)
                    _extra = $"--cached {change.DataForAmend.ParentSHA}";
                else
                    _extra = "--cached";
            }
        }

        /// <summary>
        ///     Only used for commit changes.
        /// </summary>
        /// <param name="commit"></param>
        /// <param name="change"></param>
        public DiffOption(Commit commit, Change change)
        {
            var baseRevision = commit.Parents.Count == 0 ? Commit.EmptyTreeSHA1 : $"{commit.SHA}^";
            _revisions.Add(baseRevision);
            _revisions.Add(commit.SHA);
            _path = change.Path;
            _orgPath = change.OriginalPath;
        }

        /// <summary>
        ///     Diff with filepath. Used by FileHistories
        /// </summary>
        /// <param name="commit"></param>
        /// <param name="file"></param>
        public DiffOption(Commit commit, string file)
        {
            var baseRevision = commit.Parents.Count == 0 ? Commit.EmptyTreeSHA1 : $"{commit.SHA}^";
            _revisions.Add(baseRevision);
            _revisions.Add(commit.SHA);
            _path = file;
        }

        /// <summary>
        ///     Used to diff in `FileHistory`
        /// </summary>
        /// <param name="ver"></param>
        public DiffOption(FileVersion ver)
        {
            _revisions.Add(ver.HasParent ? $"{ver.SHA}^" : Commit.EmptyTreeSHA1);
            _revisions.Add(ver.SHA);
            _path = ver.Path;
            _orgPath = ver.Change.OriginalPath;
        }

        /// <summary>
        ///     Used to diff two revisions in `FileHistory`
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        public DiffOption(FileVersion start, FileVersion end)
        {
            if (start.Change.Index == ChangeState.Deleted)
            {
                _revisions.Add(Commit.EmptyTreeSHA1);
                _revisions.Add(end.SHA);
                _path = end.Path;
            }
            else if (end.Change.Index == ChangeState.Deleted)
            {
                _revisions.Add(start.SHA);
                _revisions.Add(Commit.EmptyTreeSHA1);
                _path = start.Path;
            }
            else if (!end.Path.Equals(start.Path, StringComparison.Ordinal))
            {
                _revisions.Add($"{start.SHA}:{start.Path.Quoted()}");
                _revisions.Add($"{end.SHA}:{end.Path.Quoted()}");
                _path = end.Path;
                _orgPath = start.Path;
                _ignorePaths = true;
            }
            else
            {
                _revisions.Add(start.SHA);
                _revisions.Add(end.SHA);
                _path = start.Path;
            }
        }

        /// <summary>
        ///     Used to show differences between two revisions.
        /// </summary>
        /// <param name="baseRevision"></param>
        /// <param name="targetRevision"></param>
        /// <param name="change"></param>
        public DiffOption(string baseRevision, string targetRevision, Change change)
        {
            _revisions.Add(string.IsNullOrEmpty(baseRevision) ? "-R" : baseRevision);
            _revisions.Add(targetRevision);
            _path = change.Path;
            _orgPath = change.OriginalPath;
        }

        /// <summary>
        ///     Converts to diff command arguments.
        /// </summary>
        public override string ToString()
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrEmpty(_extra))
                builder.Append($"{_extra} ");
            foreach (var r in _revisions)
                builder.Append($"{r} ");

            if (_ignorePaths)
                return builder.ToString();

            builder.Append("-- ");
            if (!string.IsNullOrEmpty(_orgPath))
                builder.Append($"{_orgPath.Quoted()} ");
            builder.Append(_path.Quoted());

            return builder.ToString();
        }

        private readonly Change _workingCopyChange = null;
        private readonly bool _isUnstaged = false;
        private readonly string _path;
        private readonly string _orgPath = string.Empty;
        private readonly string _extra = string.Empty;
        private readonly List<string> _revisions = [];
        private readonly bool _ignorePaths = false;
    }
}
