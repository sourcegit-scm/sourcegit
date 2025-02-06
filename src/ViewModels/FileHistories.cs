using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Avalonia.Media.Imaging;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class FileHistoriesRevisionFile(string path, object content)
    {
        public string Path { get; set; } = path;
        public object Content { get; set; } = content;
    }

    public partial class FileHistories : ObservableObject
    {
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public List<Models.Commit> Commits
        {
            get => _commits;
            set => SetProperty(ref _commits, value);
        }

        public List<Models.Commit> SelectedCommits
        {
            get => _selectedCommits;
            set
            {
                if (SetProperty(ref _selectedCommits, value))
                    RefreshViewContent();
            }
        }

        public bool IsViewContent
        {
            get => _isViewContent;
            set
            {
                if (SetProperty(ref _isViewContent, value))
                    RefreshViewContent();
            }
        }

        public Models.Commit StartPoint
        {
            get => _startPoint;
            set => SetProperty(ref _startPoint, value);
        }

        public Models.Commit EndPoint
        {
            get => _endPoint;
            set => SetProperty(ref _endPoint, value);
        }

        public object ViewContent
        {
            get => _viewContent;
            private set => SetProperty(ref _viewContent, value);
        }

        public FileHistories(Repository repo, string file, string commit = null)
        {
            _repo = repo;
            _file = file;

            Task.Run(() =>
            {
                var based = commit ?? string.Empty;
                var commits = new Commands.QueryCommits(_repo.FullPath, $"--date-order -n 10000 {based} -- \"{file}\"", false).Result();
                Dispatcher.UIThread.Invoke(() =>
                {
                    IsLoading = false;
                    Commits = commits;
                    if (commits.Count > 0)
                        SelectedCommits = [commits[0]];
                });
            });
        }

        public void NavigateToCommit(Models.Commit commit)
        {
            _repo.NavigateToCommit(commit.SHA);
        }

        public void ResetToSelectedRevision()
        {
            if (_selectedCommits is not { Count: 1 })
                return;
            new Commands.Checkout(_repo.FullPath).FileWithRevision(_file, $"{_selectedCommits[0].SHA}");
        }

        public void Swap()
        {
            if (_selectedCommits is not { Count: 2 })
                return;

            (_selectedCommits[0], _selectedCommits[1]) = (_selectedCommits[1], _selectedCommits[0]);
            RefreshViewContent();
        }

        public Task<bool> SaveAsPatch(string saveTo)
        {
            return Task.Run(() =>
            {
                Commands.SaveChangesAsPatch.ProcessRevisionCompareChanges(_repo.FullPath, _changes, GetSHA(_startPoint), GetSHA(_endPoint), saveTo);
                return true;
            });
        }

        private void RefreshViewContent()
        {
            if (_selectedCommits == null || _selectedCommits.Count == 0)
            {
                StartPoint = null;
                EndPoint = null;
                ViewContent = 0;
                return;
            }

            if (_isViewContent && _selectedCommits.Count == 1)
                SetViewContentAsRevisionFile();
            else
                SetViewContentAsDiff();
        }

        private void SetViewContentAsRevisionFile()
        {
            StartPoint = null;
            EndPoint = null;
            var selectedCommit = _selectedCommits[0];
            var objs = new Commands.QueryRevisionObjects(_repo.FullPath, selectedCommit.SHA, _file).Result();
            if (objs.Count == 0)
            {
                ViewContent = new FileHistoriesRevisionFile(_file, null);
                return;
            }

            var obj = objs[0];
            switch (obj.Type)
            {
                case Models.ObjectType.Blob:
                    Task.Run(() =>
                    {
                        var isBinary = new Commands.IsBinary(_repo.FullPath, selectedCommit.SHA, _file).Result();
                        if (isBinary)
                        {
                            var ext = Path.GetExtension(_file);
                            if (IMG_EXTS.Contains(ext))
                            {
                                var stream = Commands.QueryFileContent.Run(_repo.FullPath, selectedCommit.SHA, _file);
                                var fileSize = stream.Length;
                                var bitmap = fileSize > 0 ? new Bitmap(stream) : null;
                                var imageType = Path.GetExtension(_file).TrimStart('.').ToUpper(CultureInfo.CurrentCulture);
                                var image = new Models.RevisionImageFile() { Image = bitmap, FileSize = fileSize, ImageType = imageType };
                                Dispatcher.UIThread.Invoke(() => ViewContent = new FileHistoriesRevisionFile(_file, image));
                            }
                            else
                            {
                                var size = new Commands.QueryFileSize(_repo.FullPath, _file, selectedCommit.SHA).Result();
                                var binaryFile = new Models.RevisionBinaryFile() { Size = size };
                                Dispatcher.UIThread.Invoke(() => ViewContent = new FileHistoriesRevisionFile(_file, binaryFile));
                            }

                            return;
                        }

                        var contentStream = Commands.QueryFileContent.Run(_repo.FullPath, selectedCommit.SHA, _file);
                        var content = new StreamReader(contentStream).ReadToEnd();
                        var matchLFS = REG_LFS_FORMAT().Match(content);
                        if (matchLFS.Success)
                        {
                            var lfs = new Models.RevisionLFSObject() { Object = new Models.LFSObject() };
                            lfs.Object.Oid = matchLFS.Groups[1].Value;
                            lfs.Object.Size = long.Parse(matchLFS.Groups[2].Value);
                            Dispatcher.UIThread.Invoke(() => ViewContent = new FileHistoriesRevisionFile(_file, lfs));
                        }
                        else
                        {
                            var txt = new Models.RevisionTextFile() { FileName = obj.Path, Content = content };
                            Dispatcher.UIThread.Invoke(() => ViewContent = new FileHistoriesRevisionFile(_file, txt));
                        }
                    });
                    break;
                case Models.ObjectType.Commit:
                    Task.Run(() =>
                    {
                        var submoduleRoot = Path.Combine(_repo.FullPath, _file);
                        var commit = new Commands.QuerySingleCommit(submoduleRoot, obj.SHA).Result();
                        if (commit != null)
                        {
                            var message = new Commands.QueryCommitFullMessage(submoduleRoot, obj.SHA).Result();
                            var module = new Models.RevisionSubmodule() { Commit = commit, FullMessage = message };
                            Dispatcher.UIThread.Invoke(() => ViewContent = new FileHistoriesRevisionFile(_file, module));
                        }
                        else
                        {
                            var module = new Models.RevisionSubmodule() { Commit = new Models.Commit() { SHA = obj.SHA }, FullMessage = "" };
                            Dispatcher.UIThread.Invoke(() => ViewContent = new FileHistoriesRevisionFile(_file, module));
                        }
                    });
                    break;
                default:
                    ViewContent = new FileHistoriesRevisionFile(_file, null);
                    break;
            }
        }

        private void SetViewContentAsDiff()
        {
            if (_selectedCommits is { Count: 1 })
            {
                StartPoint = null;
                EndPoint = null;
                var option = new Models.DiffOption(_selectedCommits[0], _file);
                ViewContent = new DiffContext(_repo.FullPath, option, _viewContent as DiffContext);
            }
            else if (_selectedCommits is { Count: 2 })
            {
                StartPoint = _selectedCommits[0];
                EndPoint = _selectedCommits[1];
                _changes = new Commands.CompareRevisions(_repo.FullPath, GetSHA(_selectedCommits[0]), GetSHA(_selectedCommits[1]), _file).Result();
                if (_changes.Count == 0)
                {
                    ViewContent = null;
                    return;
                }
                var option = new Models.DiffOption(GetSHA(_selectedCommits[0]), GetSHA(_selectedCommits[1]), _changes[0]);
                ViewContent = new DiffContext(_repo.FullPath, option, _viewContent as DiffContext);
            }
            else
            {
                ViewContent = _selectedCommits.Count;
            }
        }

        private string GetSHA(object obj)
        {
            return obj is Models.Commit commit ? commit.SHA : string.Empty;
        }

        [GeneratedRegex(@"^version https://git-lfs.github.com/spec/v\d+\r?\noid sha256:([0-9a-f]+)\r?\nsize (\d+)[\r\n]*$")]
        private static partial Regex REG_LFS_FORMAT();

        private static readonly HashSet<string> IMG_EXTS = new HashSet<string>()
        {
            ".ico", ".bmp", ".jpg", ".png", ".jpeg", ".webp"
        };

        private readonly Repository _repo = null;
        private readonly string _file = null;
        private bool _isLoading = true;
        private List<Models.Commit> _commits = null;
        private List<Models.Commit> _selectedCommits = [];
        private bool _isViewContent = false;
        private object _viewContent = null;
        private Models.Commit _startPoint = null;
        private Models.Commit _endPoint = null;
        private List<Models.Change> _changes = null;
    }
}
