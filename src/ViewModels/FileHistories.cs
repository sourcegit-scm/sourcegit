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

        public Models.Commit SelectedCommit
        {
            get => _selectedCommit;
            set
            {
                if (SetProperty(ref _selectedCommit, value))
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
                        SelectedCommit = commits[0];
                });
            });
        }

        public void NavigateToCommit(Models.Commit commit)
        {
            _repo.NavigateToCommit(commit.SHA);
        }

        public void ResetToSelectedRevision()
        {
            new Commands.Checkout(_repo.FullPath).FileWithRevision(_file, $"{_selectedCommit.SHA}");
        }

        private void RefreshViewContent()
        {
            if (_selectedCommit == null)
            {
                ViewContent = null;
                return;
            }

            if (_isViewContent)
                SetViewContentAsRevisionFile();
            else
                SetViewContentAsDiff();
        }

        private void SetViewContentAsRevisionFile()
        {
            var objs = new Commands.QueryRevisionObjects(_repo.FullPath, _selectedCommit.SHA, _file).Result();
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
                        var isBinary = new Commands.IsBinary(_repo.FullPath, _selectedCommit.SHA, _file).Result();
                        if (isBinary)
                        {
                            var ext = Path.GetExtension(_file);
                            if (IMG_EXTS.Contains(ext))
                            {
                                var stream = Commands.QueryFileContent.Run(_repo.FullPath, _selectedCommit.SHA, _file);
                                var fileSize = stream.Length;
                                var bitmap = fileSize > 0 ? new Bitmap(stream) : null;
                                var imageType = Path.GetExtension(_file).TrimStart('.').ToUpper(CultureInfo.CurrentCulture);
                                var image = new Models.RevisionImageFile() { Image = bitmap, FileSize = fileSize, ImageType = imageType };
                                Dispatcher.UIThread.Invoke(() => ViewContent = new FileHistoriesRevisionFile(_file, image));
                            }
                            else
                            {
                                var size = new Commands.QueryFileSize(_repo.FullPath, _file, _selectedCommit.SHA).Result();
                                var binaryFile = new Models.RevisionBinaryFile() { Size = size };
                                Dispatcher.UIThread.Invoke(() => ViewContent = new FileHistoriesRevisionFile(_file, binaryFile));
                            }

                            return;
                        }

                        var contentStream = Commands.QueryFileContent.Run(_repo.FullPath, _selectedCommit.SHA, _file);
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
            var option = new Models.DiffOption(_selectedCommit, _file);
            ViewContent = new DiffContext(_repo.FullPath, option, _viewContent as DiffContext);
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
        private Models.Commit _selectedCommit = null;
        private bool _isViewContent = false;
        private object _viewContent = null;
    }
}
