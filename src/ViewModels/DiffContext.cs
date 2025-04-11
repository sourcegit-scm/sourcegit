using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Media.Imaging;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class DiffContext : ObservableObject
    {
        public string Title
        {
            get => _title;
        }

        public bool IgnoreWhitespace
        {
            get => _ignoreWhitespace;
            set
            {
                if (SetProperty(ref _ignoreWhitespace, value))
                    LoadDiffContent();
            }
        }

        public string FileModeChange
        {
            get => _fileModeChange;
            private set => SetProperty(ref _fileModeChange, value);
        }

        public bool IsTextDiff
        {
            get => _isTextDiff;
            private set => SetProperty(ref _isTextDiff, value);
        }

        public object Content
        {
            get => _content;
            private set => SetProperty(ref _content, value);
        }

        public int UnifiedLines
        {
            get => _unifiedLines;
            private set => SetProperty(ref _unifiedLines, value);
        }

        public DiffContext(string repo, Models.DiffOption option, DiffContext previous = null)
        {
            _repo = repo;
            _option = option;

            if (previous != null)
            {
                _isTextDiff = previous._isTextDiff;
                _content = previous._content;
                _fileModeChange = previous._fileModeChange;
                _unifiedLines = previous._unifiedLines;
                _ignoreWhitespace = previous._ignoreWhitespace;
                _info = previous._info;
            }

            if (string.IsNullOrEmpty(_option.OrgPath) || _option.OrgPath == "/dev/null")
                _title = _option.Path;
            else
                _title = $"{_option.OrgPath} → {_option.Path}";

            LoadDiffContent();
        }

        public void ToggleFullTextDiff()
        {
            Preferences.Instance.UseFullTextDiff = !Preferences.Instance.UseFullTextDiff;
            LoadDiffContent();
        }

        public void IncrUnified()
        {
            UnifiedLines = _unifiedLines + 1;
            LoadDiffContent();
        }

        public void DecrUnified()
        {
            UnifiedLines = Math.Max(4, _unifiedLines - 1);
            LoadDiffContent();
        }

        public void OpenExternalMergeTool()
        {
            var toolType = Preferences.Instance.ExternalMergeToolType;
            var toolPath = Preferences.Instance.ExternalMergeToolPath;
            Task.Run(() => Commands.MergeTool.OpenForDiff(_repo, toolType, toolPath, _option));
        }

        private void LoadDiffContent()
        {
            if (_option.Path.EndsWith('/'))
            {
                Content = null;
                IsTextDiff = false;
                return;
            }

            Task.Run(() =>
            {
                // NOTE: Here we override the UnifiedLines value (if UseFullTextDiff is on).
                // There is no way to tell a git-diff to use "ALL lines of context",
                // so instead we set a very high number for the "lines of context" parameter.
                var numLines = Preferences.Instance.UseFullTextDiff ? 999999999 : _unifiedLines;
                var latest = new Commands.Diff(_repo, _option, numLines, _ignoreWhitespace).Result();
                var info = new Info(_option, numLines, _ignoreWhitespace, latest);
                if (_info != null && info.IsSame(_info))
                    return;

                _info = info;

                var rs = null as object;
                if (latest.TextDiff != null)
                {
                    var count = latest.TextDiff.Lines.Count;
                    var isSubmodule = false;
                    if (count <= 3)
                    {
                        var submoduleDiff = new Models.SubmoduleDiff();
                        var submoduleRoot = $"{_repo}/{_option.Path}".Replace("\\", "/");
                        isSubmodule = true;
                        for (int i = 1; i < count; i++)
                        {
                            var line = latest.TextDiff.Lines[i];
                            if (!line.Content.StartsWith("Subproject commit ", StringComparison.Ordinal))
                            {
                                isSubmodule = false;
                                break;
                            }

                            var sha = line.Content.Substring(18);
                            if (line.Type == Models.TextDiffLineType.Added)
                                submoduleDiff.New = QuerySubmoduleRevision(submoduleRoot, sha);
                            else if (line.Type == Models.TextDiffLineType.Deleted)
                                submoduleDiff.Old = QuerySubmoduleRevision(submoduleRoot, sha);
                        }

                        if (isSubmodule)
                            rs = submoduleDiff;
                    }

                    if (!isSubmodule)
                    {
                        latest.TextDiff.File = _option.Path;
                        rs = latest.TextDiff;
                    }
                }
                else if (latest.IsBinary)
                {
                    var oldPath = string.IsNullOrEmpty(_option.OrgPath) ? _option.Path : _option.OrgPath;
                    var ext = Path.GetExtension(_option.Path);

                    if (IMG_EXTS.Contains(ext))
                    {
                        var imgDiff = new Models.ImageDiff();
                        if (_option.Revisions.Count == 2)
                        {
                            (imgDiff.Old, imgDiff.OldFileSize) = BitmapFromRevisionFile(_repo, _option.Revisions[0], oldPath);
                            (imgDiff.New, imgDiff.NewFileSize) = BitmapFromRevisionFile(_repo, _option.Revisions[1], _option.Path);
                        }
                        else
                        {
                            if (!oldPath.Equals("/dev/null", StringComparison.Ordinal))
                                (imgDiff.Old, imgDiff.OldFileSize) = BitmapFromRevisionFile(_repo, "HEAD", oldPath);

                            var fullPath = Path.Combine(_repo, _option.Path);
                            if (File.Exists(fullPath))
                            {
                                imgDiff.New = new Bitmap(fullPath);
                                imgDiff.NewFileSize = new FileInfo(fullPath).Length;
                            }
                        }
                        rs = imgDiff;
                    }
                    else
                    {
                        var binaryDiff = new Models.BinaryDiff();
                        if (_option.Revisions.Count == 2)
                        {
                            binaryDiff.OldSize = new Commands.QueryFileSize(_repo, oldPath, _option.Revisions[0]).Result();
                            binaryDiff.NewSize = new Commands.QueryFileSize(_repo, _option.Path, _option.Revisions[1]).Result();
                        }
                        else
                        {
                            var fullPath = Path.Combine(_repo, _option.Path);
                            binaryDiff.OldSize = new Commands.QueryFileSize(_repo, oldPath, "HEAD").Result();
                            binaryDiff.NewSize = File.Exists(fullPath) ? new FileInfo(fullPath).Length : 0;
                        }
                        rs = binaryDiff;
                    }
                }
                else if (latest.IsLFS)
                {
                    rs = latest.LFSDiff;
                }
                else
                {
                    rs = new Models.NoOrEOLChange();
                }

                Dispatcher.UIThread.Post(() =>
                {
                    if (_content is Models.TextDiff old && rs is Models.TextDiff cur && old.File == cur.File)
                        cur.ScrollOffset = old.ScrollOffset;

                    FileModeChange = latest.FileModeChange;
                    Content = rs;
                    IsTextDiff = rs is Models.TextDiff;
                });
            });
        }

        private (Bitmap, long) BitmapFromRevisionFile(string repo, string revision, string file)
        {
            var stream = Commands.QueryFileContent.Run(repo, revision, file);
            var size = stream.Length;
            return size > 0 ? (new Bitmap(stream), size) : (null, size);
        }

        private Models.RevisionSubmodule QuerySubmoduleRevision(string repo, string sha)
        {
            var commit = new Commands.QuerySingleCommit(repo, sha).Result();
            if (commit != null)
            {
                var body = new Commands.QueryCommitFullMessage(repo, sha).Result();
                return new Models.RevisionSubmodule()
                {
                    Commit = commit,
                    FullMessage = new Models.CommitFullMessage { Message = body }
                };
            }

            return new Models.RevisionSubmodule()
            {
                Commit = new Models.Commit() { SHA = sha },
                FullMessage = null,
            };
        }

        private static readonly HashSet<string> IMG_EXTS = new HashSet<string>()
        {
            ".ico", ".bmp", ".jpg", ".png", ".jpeg", ".webp"
        };

        private class Info
        {
            public string Argument { get; set; }
            public int UnifiedLines { get; set; }
            public bool IgnoreWhitespace { get; set; }
            public string OldHash { get; set; }
            public string NewHash { get; set; }

            public Info(Models.DiffOption option, int unifiedLines, bool ignoreWhitespace, Models.DiffResult result)
            {
                Argument = option.ToString();
                UnifiedLines = unifiedLines;
                IgnoreWhitespace = ignoreWhitespace;
                OldHash = result.OldHash;
                NewHash = result.NewHash;
            }

            public bool IsSame(Info other)
            {
                return Argument.Equals(other.Argument, StringComparison.Ordinal) &&
                    UnifiedLines == other.UnifiedLines &&
                    IgnoreWhitespace == other.IgnoreWhitespace &&
                    OldHash.Equals(other.OldHash, StringComparison.Ordinal) &&
                    NewHash.Equals(other.NewHash, StringComparison.Ordinal);
            }
        }

        private readonly string _repo;
        private readonly Models.DiffOption _option = null;
        private string _title;
        private string _fileModeChange = string.Empty;
        private int _unifiedLines = 4;
        private bool _isTextDiff = false;
        private bool _ignoreWhitespace = false;
        private object _content = null;
        private Info _info = null;
    }
}
