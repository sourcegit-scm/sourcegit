using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Collections;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class FileHistoriesRevisionFile(string path, object content = null, bool canOpenWithDefaultEditor = false)
    {
        public string Path { get; set; } = path;
        public object Content { get; set; } = content;
        public bool CanOpenWithDefaultEditor { get; set; } = canOpenWithDefaultEditor;
    }

    public class FileHistoriesSingleRevision : ObservableObject
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

        public FileHistoriesSingleRevision(string repo, Models.FileVersion revision, bool prevIsDiffMode)
        {
            _repo = repo;
            _file = revision.Path;
            _revision = revision;
            _isDiffMode = prevIsDiffMode;
            _viewContent = null;

            RefreshViewContent();
        }

        public async Task<bool> ResetToSelectedRevisionAsync()
        {
            return await new Commands.Checkout(_repo)
                .FileWithRevisionAsync(_file, $"{_revision.SHA}")
                .ConfigureAwait(false);
        }

        public async Task OpenWithDefaultEditorAsync()
        {
            if (_viewContent is not FileHistoriesRevisionFile { CanOpenWithDefaultEditor: true })
                return;

            var fullPath = Native.OS.GetAbsPath(_repo, _file);
            var fileName = Path.GetFileNameWithoutExtension(fullPath) ?? "";
            var fileExt = Path.GetExtension(fullPath) ?? "";
            var tmpFile = Path.Combine(Path.GetTempPath(), $"{fileName}~{_revision.SHA.AsSpan(0, 10)}{fileExt}");

            await Commands.SaveRevisionFile
                .RunAsync(_repo, _revision.SHA, _file, tmpFile)
                .ConfigureAwait(false);

            Native.OS.OpenWithDefaultEditor(tmpFile);
        }

        private void RefreshViewContent()
        {
            if (_isDiffMode)
            {
                SetViewContentAsDiff();
                return;
            }

            Task.Run(async () =>
            {
                var objs = await new Commands.QueryRevisionObjects(_repo, _revision.SHA, _file)
                    .GetResultAsync()
                    .ConfigureAwait(false);

                if (objs.Count == 0)
                {
                    Dispatcher.UIThread.Post(() => ViewContent = new FileHistoriesRevisionFile(_file));
                    return;
                }

                var revisionContent = await GetRevisionFileContentAsync(objs[0]).ConfigureAwait(false);
                Dispatcher.UIThread.Post(() => ViewContent = revisionContent);
            });
        }

        private async Task<object> GetRevisionFileContentAsync(Models.Object obj)
        {
            if (obj.Type == Models.ObjectType.Blob)
            {
                var isBinary = await new Commands.IsBinary(_repo, _revision.SHA, _file).GetResultAsync().ConfigureAwait(false);
                if (isBinary)
                {
                    var imgDecoder = ImageSource.GetDecoder(_file);
                    if (imgDecoder != Models.ImageDecoder.None)
                    {
                        var source = await ImageSource.FromRevisionAsync(_repo, _revision.SHA, _file, imgDecoder).ConfigureAwait(false);
                        var image = new Models.RevisionImageFile(_file, source.Bitmap, source.Size);
                        return new FileHistoriesRevisionFile(_file, image, true);
                    }

                    var size = await new Commands.QueryFileSize(_repo, _file, _revision.SHA).GetResultAsync().ConfigureAwait(false);
                    var binaryFile = new Models.RevisionBinaryFile() { Size = size };
                    return new FileHistoriesRevisionFile(_file, binaryFile, true);
                }

                var contentStream = await Commands.QueryFileContent.RunAsync(_repo, _revision.SHA, _file).ConfigureAwait(false);
                var content = await new StreamReader(contentStream).ReadToEndAsync();
                var lfs = Models.LFSObject.Parse(content);
                if (lfs != null)
                {
                    var imgDecoder = ImageSource.GetDecoder(_file);
                    if (imgDecoder != Models.ImageDecoder.None)
                    {
                        var combined = new RevisionLFSImage(_repo, _file, lfs, imgDecoder);
                        return new FileHistoriesRevisionFile(_file, combined, true);
                    }

                    var rlfs = new Models.RevisionLFSObject() { Object = lfs };
                    return new FileHistoriesRevisionFile(_file, rlfs, true);
                }

                var txt = new Models.RevisionTextFile() { FileName = obj.Path, Content = content };
                return new FileHistoriesRevisionFile(_file, txt, true);
            }

            if (obj.Type == Models.ObjectType.Commit)
            {
                var submoduleRoot = Path.Combine(_repo, _file);
                var commit = await new Commands.QuerySingleCommit(submoduleRoot, obj.SHA).GetResultAsync().ConfigureAwait(false);
                var message = commit != null ? await new Commands.QueryCommitFullMessage(submoduleRoot, obj.SHA).GetResultAsync().ConfigureAwait(false) : null;
                var module = new Models.RevisionSubmodule()
                {
                    Commit = commit ?? new Models.Commit() { SHA = obj.SHA },
                    FullMessage = new Models.CommitFullMessage { Message = message }
                };

                return new FileHistoriesRevisionFile(_file, module);
            }

            return new FileHistoriesRevisionFile(_file);
        }

        private void SetViewContentAsDiff()
        {
            ViewContent = new DiffContext(_repo, new Models.DiffOption(_revision), _viewContent as DiffContext);
        }

        private string _repo = null;
        private string _file = null;
        private Models.FileVersion _revision = null;
        private bool _isDiffMode = false;
        private object _viewContent = null;
    }

    public class FileHistoriesCompareRevisions : ObservableObject
    {
        public Models.FileVersion StartPoint
        {
            get => _startPoint;
            set => SetProperty(ref _startPoint, value);
        }

        public Models.FileVersion EndPoint
        {
            get => _endPoint;
            set => SetProperty(ref _endPoint, value);
        }

        public DiffContext ViewContent
        {
            get => _viewContent;
            set => SetProperty(ref _viewContent, value);
        }

        public FileHistoriesCompareRevisions(string repo, Models.FileVersion start, Models.FileVersion end)
        {
            _repo = repo;
            _startPoint = start;
            _endPoint = end;
            _viewContent = new(_repo, new(start, end));
        }

        public void Swap()
        {
            (StartPoint, EndPoint) = (_endPoint, _startPoint);
            ViewContent = new(_repo, new(_startPoint, _endPoint), _viewContent);
        }

        public async Task<bool> SaveAsPatch(string saveTo)
        {
            return await Commands.SaveChangesAsPatch
                .ProcessRevisionCompareChangesAsync(_repo, _changes, _startPoint.SHA, _endPoint.SHA, saveTo)
                .ConfigureAwait(false);
        }

        private string _repo = null;
        private Models.FileVersion _startPoint = null;
        private Models.FileVersion _endPoint = null;
        private List<Models.Change> _changes = [];
        private DiffContext _viewContent = null;
    }

    public class FileHistories : ObservableObject
    {
        public string Title
        {
            get;
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public List<Models.FileVersion> Revisions
        {
            get => _revisions;
            set => SetProperty(ref _revisions, value);
        }

        public AvaloniaList<Models.FileVersion> SelectedRevisions
        {
            get;
            set;
        } = [];

        public object ViewContent
        {
            get => _viewContent;
            private set => SetProperty(ref _viewContent, value);
        }

        public FileHistories(string repo, string file, string commit = null)
        {
            if (!string.IsNullOrEmpty(commit))
                Title = $"{file} @ {commit}";
            else
                Title = file;

            _repo = repo;

            Task.Run(async () =>
            {
                var revisions = await new Commands.QueryFileHistory(_repo, file, commit)
                    .GetResultAsync()
                    .ConfigureAwait(false);

                Dispatcher.UIThread.Post(() =>
                {
                    IsLoading = false;
                    Revisions = revisions;
                    if (revisions.Count > 0)
                        SelectedRevisions.Add(revisions[0]);
                });
            });

            SelectedRevisions.CollectionChanged += (_, _) =>
            {
                if (_viewContent is FileHistoriesSingleRevision singleRevision)
                    _prevIsDiffMode = singleRevision.IsDiffMode;

                ViewContent = SelectedRevisions.Count switch
                {
                    1 => new FileHistoriesSingleRevision(_repo, SelectedRevisions[0], _prevIsDiffMode),
                    2 => new FileHistoriesCompareRevisions(_repo, SelectedRevisions[0], SelectedRevisions[1]),
                    _ => SelectedRevisions.Count,
                };
            };
        }

        public void NavigateToCommit(Models.FileVersion revision)
        {
            var launcher = App.GetLauncher();
            if (launcher != null)
            {
                foreach (var page in launcher.Pages)
                {
                    if (page.Data is Repository repo && repo.FullPath.Equals(_repo, StringComparison.Ordinal))
                    {
                        repo.NavigateToCommit(revision.SHA);
                        break;
                    }
                }
            }
        }

        public string GetCommitFullMessage(Models.FileVersion revision)
        {
            var sha = revision.SHA;
            if (_fullCommitMessages.TryGetValue(sha, out var msg))
                return msg;

            msg = new Commands.QueryCommitFullMessage(_repo, sha).GetResult();
            _fullCommitMessages[sha] = msg;
            return msg;
        }

        private readonly string _repo = null;
        private bool _isLoading = true;
        private bool _prevIsDiffMode = true;
        private List<Models.FileVersion> _revisions = null;
        private Dictionary<string, string> _fullCommitMessages = new();
        private object _viewContent = null;
    }
}
