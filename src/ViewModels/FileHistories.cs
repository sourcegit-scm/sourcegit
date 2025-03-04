using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Avalonia.Collections;
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

    public partial class FileHistoriesSingleRevision : ObservableObject
    {
        public bool IsDiffMode
        {
            get => _isDiffMode;
            set
            {
                if (SetProperty(ref _isDiffMode, value))
                    RefreshViewContent();
            }
        }

        public object ViewContent
        {
            get => _viewContent;
            set => SetProperty(ref _viewContent, value);
        }

        public FileHistoriesSingleRevision(Repository repo, string file, Models.Commit revision, bool prevIsDiffMode)
        {
            _repo = repo;
            _file = file;
            _revision = revision;
            _isDiffMode = prevIsDiffMode;
            _viewContent = null;

            RefreshViewContent();
        }

        public void ResetToSelectedRevision()
        {
            new Commands.Checkout(_repo.FullPath).FileWithRevision(_file, $"{_revision.SHA}");
        }

        private void RefreshViewContent()
        {
            if (_isDiffMode)
                SetViewContentAsDiff();
            else
                SetViewContentAsRevisionFile();
        }

        private void SetViewContentAsRevisionFile()
        {
            var objs = new Commands.QueryRevisionObjects(_repo.FullPath, _revision.SHA, _file).Result();
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
                        var isBinary = new Commands.IsBinary(_repo.FullPath, _revision.SHA, _file).Result();
                        if (isBinary)
                        {
                            var ext = Path.GetExtension(_file);
                            if (IMG_EXTS.Contains(ext))
                            {
                                var stream = Commands.QueryFileContent.Run(_repo.FullPath, _revision.SHA, _file);
                                var fileSize = stream.Length;
                                var bitmap = fileSize > 0 ? new Bitmap(stream) : null;
                                var imageType = Path.GetExtension(_file).TrimStart('.').ToUpper(CultureInfo.CurrentCulture);
                                var image = new Models.RevisionImageFile() { Image = bitmap, FileSize = fileSize, ImageType = imageType };
                                Dispatcher.UIThread.Invoke(() => ViewContent = new FileHistoriesRevisionFile(_file, image));
                            }
                            else
                            {
                                var size = new Commands.QueryFileSize(_repo.FullPath, _file, _revision.SHA).Result();
                                var binaryFile = new Models.RevisionBinaryFile() { Size = size };
                                Dispatcher.UIThread.Invoke(() => ViewContent = new FileHistoriesRevisionFile(_file, binaryFile));
                            }

                            return;
                        }

                        var contentStream = Commands.QueryFileContent.Run(_repo.FullPath, _revision.SHA, _file);
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
                            var module = new Models.RevisionSubmodule()
                            {
                                Commit = commit,
                                FullMessage = new Models.CommitFullMessage { Message = message }
                            };
                            Dispatcher.UIThread.Invoke(() => ViewContent = new FileHistoriesRevisionFile(_file, module));
                        }
                        else
                        {
                            var module = new Models.RevisionSubmodule()
                            {
                                Commit = new Models.Commit() { SHA = obj.SHA },
                                FullMessage = null
                            };
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
            var option = new Models.DiffOption(_revision, _file);
            ViewContent = new DiffContext(_repo.FullPath, option, _viewContent as DiffContext);
        }

        [GeneratedRegex(@"^version https://git-lfs.github.com/spec/v\d+\r?\noid sha256:([0-9a-f]+)\r?\nsize (\d+)[\r\n]*$")]
        private static partial Regex REG_LFS_FORMAT();

        private static readonly HashSet<string> IMG_EXTS = new HashSet<string>()
        {
            ".ico", ".bmp", ".jpg", ".png", ".jpeg", ".webp"
        };

        private Repository _repo = null;
        private string _file = null;
        private Models.Commit _revision = null;
        private bool _isDiffMode = true;
        private object _viewContent = null;
    }

    public class FileHistoriesCompareRevisions : ObservableObject
    {
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

        public DiffContext ViewContent
        {
            get => _viewContent;
            set => SetProperty(ref _viewContent, value);
        }

        public FileHistoriesCompareRevisions(Repository repo, string file, Models.Commit start, Models.Commit end)
        {
            _repo = repo;
            _file = file;
            _startPoint = start;
            _endPoint = end;
            RefreshViewContent();
        }

        public void Swap()
        {
            (StartPoint, EndPoint) = (_endPoint, _startPoint);
            RefreshViewContent();
        }

        public Task<bool> SaveAsPatch(string saveTo)
        {
            return Task.Run(() =>
            {
                Commands.SaveChangesAsPatch.ProcessRevisionCompareChanges(_repo.FullPath, _changes, _startPoint.SHA, _endPoint.SHA, saveTo);
                return true;
            });
        }

        private void RefreshViewContent()
        {
            Task.Run(() =>
            {
                _changes = new Commands.CompareRevisions(_repo.FullPath, _startPoint.SHA, _endPoint.SHA, _file).Result();
                if (_changes.Count == 0)
                {
                    Dispatcher.UIThread.Invoke(() => ViewContent = null);
                    return;
                }

                var option = new Models.DiffOption(_startPoint.SHA, _endPoint.SHA, _changes[0]);
                Dispatcher.UIThread.Invoke(() => ViewContent = new DiffContext(_repo.FullPath, option, _viewContent));
            });
        }

        private Repository _repo = null;
        private string _file = null;
        private Models.Commit _startPoint = null;
        private Models.Commit _endPoint = null;
        private List<Models.Change> _changes = [];
        private DiffContext _viewContent = null;
    }

    public class FileHistories : ObservableObject
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

        public AvaloniaList<Models.Commit> SelectedCommits
        {
            get;
            set;
        } = [];

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
                    if (Commits.Count > 0)
                        SelectedCommits.Add(Commits[0]);
                });
            });

            SelectedCommits.CollectionChanged += (_, _) =>
            {
                if (_viewContent is FileHistoriesSingleRevision singleRevision)
                    _prevIsDiffMode = singleRevision.IsDiffMode;

                switch (SelectedCommits.Count)
                {
                    case 1:
                        ViewContent = new FileHistoriesSingleRevision(_repo, _file, SelectedCommits[0], _prevIsDiffMode);
                        break;
                    case 2:
                        ViewContent = new FileHistoriesCompareRevisions(_repo, _file, SelectedCommits[0], SelectedCommits[1]);
                        break;
                    default:
                        ViewContent = SelectedCommits.Count;
                        break;
                }
            };
        }

        public void NavigateToCommit(Models.Commit commit)
        {
            _repo.NavigateToCommit(commit.SHA);
        }

        private readonly Repository _repo = null;
        private readonly string _file = null;
        private bool _isLoading = true;
        private bool _prevIsDiffMode = true;
        private List<Models.Commit> _commits = null;
        private object _viewContent = null;
    }
}
