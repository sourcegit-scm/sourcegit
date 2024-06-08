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
        public string RepositoryPath
        {
            get => _repo;
        }

        public Models.Change WorkingCopyChange
        {
            get => _option.WorkingCopyChange;
        }

        public bool IsUnstaged
        {
            get => _option.IsUnstaged;
        }

        public string Title
        {
            get => _title;
            private set => SetProperty(ref _title, value);
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

        public int Unified
        {
            get => _unified;
            set
            {
                if (SetProperty(ref _unified, value))
                    LoadDiffContent();
            }
        }

        public DiffContext(string repo, Models.DiffOption option, DiffContext previous = null)
        {
            _repo = repo;
            _option = option;

            if (previous != null)
            {
                _isTextDiff = previous._isTextDiff;
                _content = previous._content;
            }

            if (string.IsNullOrEmpty(_option.OrgPath) || _option.OrgPath == "/dev/null")
                _title = _option.Path;
            else
                _title = $"{_option.OrgPath} → {_option.Path}";

            LoadDiffContent();
        }

        public void IncrUnified()
        {
            Unified = _unified + 1;
        }

        public void DecrUnified()
        {
            Unified = Math.Max(4, _unified - 1);
        }

        public void OpenExternalMergeTool()
        {
            var type = Preference.Instance.ExternalMergeToolType;
            var exec = Preference.Instance.ExternalMergeToolPath;

            var tool = Models.ExternalMerger.Supported.Find(x => x.Type == type);
            if (tool == null || !File.Exists(exec))
            {
                App.RaiseException(_repo, "Invalid merge tool in preference setting!");
                return;
            }

            var args = tool.Type != 0 ? tool.DiffCmd : Preference.Instance.ExternalMergeToolDiffCmd;
            Task.Run(() => Commands.MergeTool.OpenForDiff(_repo, exec, args, _option));
        }

        private void LoadDiffContent()
        {
            Task.Run(() =>
            {
                var latest = new Commands.Diff(_repo, _option, _unified).Result();
                var rs = null as object;

                if (latest.TextDiff != null)
                {
                    var repo = Preference.FindRepository(_repo);
                    if (repo != null && repo.Submodules.Contains(_option.Path))
                    {
                        var submoduleDiff = new Models.SubmoduleDiff();
                        var submoduleRoot = $"{_repo}/{_option.Path}".Replace("\\", "/");
                        foreach (var line in latest.TextDiff.Lines)
                        {
                            if (line.Type == Models.TextDiffLineType.Added)
                            {
                                var sha = line.Content.Substring("Subproject commit ".Length);
                                submoduleDiff.New = QuerySubmoduleRevision(submoduleRoot, sha);
                            }
                            else if (line.Type == Models.TextDiffLineType.Deleted)
                            {
                                var sha = line.Content.Substring("Subproject commit ".Length);
                                submoduleDiff.Old = QuerySubmoduleRevision(submoduleRoot, sha);
                            }
                        }
                        rs = submoduleDiff;
                    }
                    else
                    {
                        latest.TextDiff.File = _option.Path;
                        rs = latest.TextDiff;
                    }
                }
                else if (latest.IsBinary)
                {
                    var oldPath = string.IsNullOrEmpty(_option.OrgPath) ? _option.Path : _option.OrgPath;
                    var ext = Path.GetExtension(oldPath);

                    if (IMG_EXTS.Contains(ext))
                    {
                        var imgDiff = new Models.ImageDiff();
                        if (_option.Revisions.Count == 2)
                        {
                            (imgDiff.Old, imgDiff.OldFileSize) = BitmapFromRevisionFile(_repo, _option.Revisions[0], oldPath);
                            (imgDiff.New, imgDiff.NewFileSize) = BitmapFromRevisionFile(_repo, _option.Revisions[1], oldPath);
                        }
                        else
                        {
                            var fullPath = Path.Combine(_repo, _option.Path);
                            (imgDiff.Old, imgDiff.OldFileSize) = BitmapFromRevisionFile(_repo, "HEAD", oldPath);

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

        private Models.SubmoduleRevision QuerySubmoduleRevision(string repo, string sha)
        {
            var commit = new Commands.QuerySingleCommit(repo, sha).Result();
            if (commit != null)
            {
                var body = new Commands.QueryCommitFullMessage(repo, sha).Result();
                return new Models.SubmoduleRevision() { Commit = commit, FullMessage = body };
            }

            return new Models.SubmoduleRevision()
            {
                Commit = new Models.Commit() { SHA = sha },
                FullMessage = string.Empty,
            };
        }

        private static readonly HashSet<string> IMG_EXTS = new HashSet<string>()
        {
            ".ico", ".bmp", ".jpg", ".png", ".jpeg"
        };

        private readonly string _repo = string.Empty;
        private readonly Models.DiffOption _option = null;
        private string _title = string.Empty;
        private string _fileModeChange = string.Empty;
        private bool _isLoading = true;
        private bool _isTextDiff = false;
        private object _content = null;
        private int _unified = 4;
    }
}
