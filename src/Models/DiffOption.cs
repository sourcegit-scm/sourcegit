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

            if (isUnstaged)
            {
                switch (change.WorkTree)
                {
                    case ChangeState.Added:
                    case ChangeState.Untracked:
                        _extra = "--no-index";
                        _path = change.Path;
                        _orgPath = "/dev/null";
                        break;
                    default:
                        _path = change.Path;
                        _orgPath = change.OriginalPath;
                        break;
                }
            }
            else
            {
                if (change.DataForAmend != null)
                    _extra = $"--cached {change.DataForAmend.ParentSHA}";
                else
                    _extra = "--cached";

                _path = change.Path;
                _orgPath = change.OriginalPath;
            }
        }

        /// <summary>
        ///     Only used for commit changes.
        /// </summary>
        /// <param name="commit"></param>
        /// <param name="change"></param>
        public DiffOption(Commit commit, Change change)
        {
            var baseRevision = commit.Parents.Count == 0 ? "4b825dc642cb6eb9a060e54bf8d69288fbee4904" : $"{commit.SHA}^";
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
            var baseRevision = commit.Parents.Count == 0 ? "4b825dc642cb6eb9a060e54bf8d69288fbee4904" : $"{commit.SHA}^";
            _revisions.Add(baseRevision);
            _revisions.Add(commit.SHA);
            _path = file;
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
        /// <returns></returns>
        public override string ToString()
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrEmpty(_extra))
                builder.Append($"{_extra} ");
            foreach (var r in _revisions)
                builder.Append($"{r} ");

            builder.Append("-- ");
            if (!string.IsNullOrEmpty(_orgPath))
                builder.Append($"\"{_orgPath}\" ");
            builder.Append($"\"{_path}\"");

            return builder.ToString();
        }

        private readonly Change _workingCopyChange = null;
        private readonly bool _isUnstaged = false;
        private readonly string _path;
        private readonly string _orgPath = string.Empty;
        private readonly string _extra = string.Empty;
        private readonly List<string> _revisions = new List<string>();
    }
}
