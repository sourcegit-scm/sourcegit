using System;
using System.Collections.Generic;
using System.Text;

namespace SourceGit.Models
{
    public class DiffOption
    {
        public bool IsLocalChange => _revisions.Count == 0;
        public bool IsUnstaged => _isUnstaged;
        public List<string> Revisions => _revisions;
        public string Path => _path;
        public string OrgPath => _orgPath;
        public CommitChange CommitChange { get; }

        /// <summary>
        ///     Only used for working copy changes
        /// </summary>
        /// <param name="change"></param>
        /// <param name="isUnstaged"></param>
        public DiffOption(Change change, bool isUnstaged)
        {
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
            _revisions.Add(commit.FirstParentToCompare);
            _revisions.Add(commit.SHA);
            _path = change.Path;
            _orgPath = change.OriginalPath;
        }

        /// <summary>
        ///     Used to diff in `FileHistory`
        /// </summary>
        /// <param name="ver"></param>
        public DiffOption(FileVersion ver)
        {
            if (string.IsNullOrEmpty(ver.OriginalPath))
            {
                _revisions.Add(ver.HasParent ? $"{ver.SHA}^" : EmptyTreeHash.Guess(ver.SHA));
                _revisions.Add(ver.SHA);
                _path = ver.Path;
            }
            else
            {
                _revisions.Add($"{ver.SHA}^:{ver.OriginalPath.Quoted()}");
                _revisions.Add($"{ver.SHA}:{ver.Path.Quoted()}");
                _path = ver.Path;
                _orgPath = ver.Change.OriginalPath;
                _ignorePaths = true;
            }
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
                _revisions.Add(EmptyTreeHash.Guess(end.SHA));
                _revisions.Add(end.SHA);
                _path = end.Path;
            }
            else if (end.Change.Index == ChangeState.Deleted)
            {
                _revisions.Add(start.SHA);
                _revisions.Add(EmptyTreeHash.Guess(start.SHA));
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
        ///     Compare the exact endpoints of a joined commit change. Keep revision IDs
        ///     separate from blob IDs so image and binary readers can resolve each path.
        /// </summary>
        public DiffOption(CommitChange change)
        {
            CommitChange = change;
            var revision = string.IsNullOrEmpty(change.BeforeRevision) ? change.AfterRevision : change.BeforeRevision;
            var emptyTree = EmptyTreeHash.Guess(revision);
            _revisions.Add(string.IsNullOrEmpty(change.BeforeRevision) ? emptyTree : change.BeforeRevision);
            _revisions.Add(string.IsNullOrEmpty(change.AfterRevision) ? emptyTree : change.AfterRevision);
            _path = string.IsNullOrEmpty(change.AfterPath) ? change.BeforePath : change.AfterPath;
            _orgPath = !string.IsNullOrEmpty(change.BeforePath) && change.BeforePath != _path ? change.BeforePath : string.Empty;

            var hasBefore = !string.IsNullOrEmpty(change.BeforeRevision) && change.BeforeMode != "160000";
            var hasAfter = !string.IsNullOrEmpty(change.AfterRevision) && change.AfterMode != "160000";
            if (hasBefore && hasAfter && change.BeforePath.Equals(change.AfterPath, StringComparison.Ordinal))
                _endpointArguments = $"{change.BeforeRevision} {change.AfterRevision} -- {QuoteEndpointPath(change.AfterPath)}";
            else if (hasBefore && hasAfter)
                _endpointArguments = $"{change.BeforeObject} {change.AfterObject}";
            else if (hasBefore)
                _endpointArguments = $"{change.BeforeRevision} {emptyTree} -- {QuoteEndpointPath(change.BeforePath)}";
            else if (hasAfter)
                _endpointArguments = $"{emptyTree} {change.AfterRevision} -- {QuoteEndpointPath(change.AfterPath)}";
            else
                _endpointArguments = $"{_revisions[0]} {_revisions[1]} -- {QuoteEndpointPath(_path)}";
        }

        /// <summary>
        ///     Converts to diff command arguments.
        /// </summary>
        public override string ToString()
        {
            if (_endpointArguments != null)
                return _endpointArguments;

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

        // ProcessStartInfo.Arguments uses command-line quoting, not shell quoting.
        // Backslashes must be doubled before a quote and before the closing quote.
        private static string QuoteEndpointPath(string path)
        {
            var builder = new StringBuilder("\":(literal)");
            var backslashes = 0;
            foreach (var c in path)
            {
                if (c == '\\')
                {
                    backslashes++;
                    continue;
                }

                builder.Append('\\', c == '\"' ? backslashes * 2 + 1 : backslashes);
                builder.Append(c);
                backslashes = 0;
            }
            builder.Append('\\', backslashes * 2);
            builder.Append('\"');
            return builder.ToString();
        }

        private readonly string _endpointArguments = null;
        private readonly bool _isUnstaged = false;
        private readonly string _path;
        private readonly string _orgPath = string.Empty;
        private readonly string _extra = string.Empty;
        private readonly List<string> _revisions = [];
        private readonly bool _ignorePaths = false;
    }
}
