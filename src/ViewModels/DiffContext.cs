using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;
using System.Threading.Tasks;

namespace SourceGit.ViewModels {
    public class DiffContext : ObservableObject {
        public string RepositoryPath {
            get => _repo;
        }

        public Models.Change WorkingCopyChange {
            get => _option.WorkingCopyChange;
        }

        public bool IsUnstaged {
            get => _option.IsUnstaged;
        }

        public string FilePath {
            get => _option.Path;
        }

        public bool IsOrgFilePathVisible {
            get => !string.IsNullOrWhiteSpace(_option.OrgPath) && _option.OrgPath != "/dev/null";
        }

        public string OrgFilePath {
            get => _option.OrgPath;
        }

        public bool IsLoading {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public bool IsNoChange {
            get => _isNoChange;
            private set => SetProperty(ref _isNoChange, value);
        }

        public bool IsTextDiff {
            get => _isTextDiff;
            private set => SetProperty(ref _isTextDiff, value);
        }

        public object Content {
            get => _content;
            private set => SetProperty(ref _content, value);
        }

        public DiffContext(string repo, Models.DiffOption option) {
            _repo = repo;
            _option = option;

            OnPropertyChanged(nameof(FilePath));
            OnPropertyChanged(nameof(IsOrgFilePathVisible));
            OnPropertyChanged(nameof(OrgFilePath));

            Task.Run(() => {
                var latest = new Commands.Diff(repo, option).Result();
                var binaryDiff = null as Models.BinaryDiff;

                if (latest.IsBinary) {
                    binaryDiff = new Models.BinaryDiff();

                    var oldPath = string.IsNullOrEmpty(_option.OrgPath) ? _option.Path : _option.OrgPath;
                    if (option.Revisions.Count == 2) {
                        binaryDiff.OldSize = new Commands.QueryFileSize(repo, oldPath, option.Revisions[0]).Result();
                        binaryDiff.NewSize = new Commands.QueryFileSize(repo, _option.Path, option.Revisions[1]).Result();
                    } else {
                        binaryDiff.OldSize = new Commands.QueryFileSize(repo, oldPath, "HEAD").Result();
                        binaryDiff.NewSize = new FileInfo(Path.Combine(repo, _option.Path)).Length;
                    }
                }

                Dispatcher.UIThread.InvokeAsync(() => {
                    if (latest.IsBinary) {
                        Content = binaryDiff;
                    } else if (latest.IsLFS) {
                        Content = latest.LFSDiff;
                    } else if (latest.TextDiff != null) {
                        latest.TextDiff.File = _option.Path;
                        Content = latest.TextDiff;
                        IsTextDiff = true;
                    } else {
                        IsTextDiff = false;
                        IsNoChange = true;
                    }

                    IsLoading = false;
                });
            });
        }

        public async void OpenExternalMergeTool() {
            var type = Preference.Instance.ExternalMergeToolType;
            var exec = Preference.Instance.ExternalMergeToolPath;

            var tool = Models.ExternalMergeTools.Supported.Find(x => x.Type == type);
            if (tool == null || !File.Exists(exec)) {
                App.RaiseException(_repo, "Invalid merge tool in preference setting!");
                return;
            }

            var args = tool.Type != 0 ? tool.DiffCmd : Preference.Instance.ExternalMergeToolDiffCmd;
            await Task.Run(() => Commands.MergeTool.OpenForDiff(_repo, exec, args, _option));
        }

        private string _repo = string.Empty;
        private Models.DiffOption _option = null;
        private bool _isLoading = true;
        private bool _isNoChange = false;
        private bool _isTextDiff = false;
        private object _content = null;
    }
}
