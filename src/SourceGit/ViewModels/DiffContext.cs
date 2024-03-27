using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Avalonia;
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

        public string FilePath
        {
            get => _option.Path;
        }

        public bool IsOrgFilePathVisible
        {
            get => !string.IsNullOrWhiteSpace(_option.OrgPath) && _option.OrgPath != "/dev/null";
        }

        public string OrgFilePath
        {
            get => _option.OrgPath;
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

        public Vector SyncScrollOffset
        {
            get => _syncScrollOffset;
            set => SetProperty(ref _syncScrollOffset, value);
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

            OnPropertyChanged(nameof(FilePath));
            OnPropertyChanged(nameof(IsOrgFilePathVisible));
            OnPropertyChanged(nameof(OrgFilePath));

            Task.Run(() =>
            {
                var latest = new Commands.Diff(repo, option).Result();
                var rs = null as object;

                if (latest.TextDiff != null)
                {
                    latest.TextDiff.File = _option.Path;
                    rs = latest.TextDiff;
                }
                else if (latest.IsBinary)
                {
                    var oldPath = string.IsNullOrEmpty(_option.OrgPath) ? _option.Path : _option.OrgPath;
                    var ext = Path.GetExtension(oldPath);

                    if (IMG_EXTS.Contains(ext))
                    {
                        var imgDiff = new Models.ImageDiff();
                        if (option.Revisions.Count == 2)
                        {
                            imgDiff.Old = Commands.GetImageFileAsBitmap.Run(repo, option.Revisions[0], oldPath);
                            imgDiff.New = Commands.GetImageFileAsBitmap.Run(repo, option.Revisions[1], oldPath);
                        }
                        else
                        {
                            imgDiff.Old = Commands.GetImageFileAsBitmap.Run(repo, "HEAD", oldPath);
                            imgDiff.New = File.Exists(_option.Path) ? new Avalonia.Media.Imaging.Bitmap(_option.Path) : null;
                        }
                        rs = imgDiff;
                    }
                    else
                    {
                        var binaryDiff = new Models.BinaryDiff();
                        if (option.Revisions.Count == 2)
                        {
                            binaryDiff.OldSize = new Commands.QueryFileSize(repo, oldPath, option.Revisions[0]).Result();
                            binaryDiff.NewSize = new Commands.QueryFileSize(repo, _option.Path, option.Revisions[1]).Result();
                        }
                        else
                        {
                            binaryDiff.OldSize = new Commands.QueryFileSize(repo, oldPath, "HEAD").Result();
                            binaryDiff.NewSize = new FileInfo(Path.Combine(repo, _option.Path)).Length;
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
                    Content = rs;
                    IsTextDiff = latest.TextDiff != null;
                    IsLoading = false;
                });
            });
        }

        public async void OpenExternalMergeTool()
        {
            var type = Preference.Instance.ExternalMergeToolType;
            var exec = Preference.Instance.ExternalMergeToolPath;

            var tool = Models.ExternalMergeTools.Supported.Find(x => x.Type == type);
            if (tool == null || !File.Exists(exec))
            {
                App.RaiseException(_repo, "Invalid merge tool in preference setting!");
                return;
            }

            var args = tool.Type != 0 ? tool.DiffCmd : Preference.Instance.ExternalMergeToolDiffCmd;
            await Task.Run(() => Commands.MergeTool.OpenForDiff(_repo, exec, args, _option));
        }

        private static readonly HashSet<string> IMG_EXTS = new HashSet<string>()
        {
            ".ico", ".bmp", ".jpg", ".png", ".jpeg"
        };

        private readonly string _repo = string.Empty;
        private readonly Models.DiffOption _option = null;
        private bool _isLoading = true;
        private bool _isTextDiff = false;
        private object _content = null;
        private Vector _syncScrollOffset = Vector.Zero;
    }
}