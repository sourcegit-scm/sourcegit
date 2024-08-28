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

        public string FileModeChange
        {
            get => _fileModeChange;
            private set => SetProperty(ref _fileModeChange, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
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
                _unifiedLines = previous._unifiedLines;
            }

            if (string.IsNullOrEmpty(_option.OrgPath) || _option.OrgPath == "/dev/null")
                _title = _option.Path;
            else
                _title = $"{_option.OrgPath} → {_option.Path}";

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
            var toolType = Preference.Instance.ExternalMergeToolType;
            var toolPath = Preference.Instance.ExternalMergeToolPath;
            Task.Run(() => Commands.MergeTool.OpenForDiff(_repo, toolType, toolPath, _option));
        }

        private void LoadDiffContent()
        {
            if (_option.Path.EndsWith('/'))
            {
                Content = null;
                IsTextDiff = false;
                IsLoading = false;
                return;
            }

            Task.Run(() =>
            {
                var latest = new Commands.Diff(_repo, _option, _unifiedLines).Result();
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
                        cur.SyncScrollOffset = old.SyncScrollOffset;

                    FileModeChange = latest.FileModeChange;
                    Content = rs;
                    IsTextDiff = rs is Models.TextDiff;
                    IsLoading = false;
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
                return new Models.RevisionSubmodule() { Commit = commit, FullMessage = body };
            }

            return new Models.RevisionSubmodule()
            {
                Commit = new Models.Commit() { SHA = sha },
                FullMessage = string.Empty,
            };
        }

        private static readonly HashSet<string> IMG_EXTS = new HashSet<string>()
        {
            ".ico", ".bmp", ".jpg", ".png", ".jpeg", ".webp"
        };

        private readonly string _repo;
        private readonly Models.DiffOption _option = null;
        private string _title;
        private string _fileModeChange = string.Empty;
        private int _unifiedLines = 4;
        private bool _isLoading = true;
        private bool _isTextDiff = false;
        private object _content = null;
    }
}
