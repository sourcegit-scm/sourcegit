﻿using System.Collections.Generic;
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

        public FileHistoriesSingleRevision(Repository repo, string file, Models.Commit revision, bool prevIsDiffMode)
        {
            _repo = repo;
            _file = file;
            _revision = revision;
            _isDiffMode = prevIsDiffMode;
            _viewContent = null;

            RefreshViewContent();
        }

        public Task<bool> ResetToSelectedRevision()
        {
            return Task.Run(async () => await new Commands.Checkout(_repo.FullPath).FileWithRevisionAsync(_file, $"{_revision.SHA}"));
        }

        public Task OpenWithDefaultEditor()
        {
            if (_viewContent is not FileHistoriesRevisionFile { CanOpenWithDefaultEditor: true })
                return null;

            return Task.Run(async () =>
            {
                var fullPath = Native.OS.GetAbsPath(_repo.FullPath, _file);
                var fileName = Path.GetFileNameWithoutExtension(fullPath) ?? "";
                var fileExt = Path.GetExtension(fullPath) ?? "";
                var tmpFile = Path.Combine(Path.GetTempPath(), $"{fileName}~{_revision.SHA.Substring(0, 10)}{fileExt}");

                await Commands.SaveRevisionFile.RunAsync(_repo.FullPath, _revision.SHA, _file, tmpFile);
                Native.OS.OpenWithDefaultEditor(tmpFile);
            });
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
                ViewContent = new FileHistoriesRevisionFile(_file);
                return;
            }

            var obj = objs[0];
            switch (obj.Type)
            {
                case Models.ObjectType.Blob:
                    Task.Run(async () =>
                    {
                        var isBinary = await new Commands.IsBinary(_repo.FullPath, _revision.SHA, _file).ResultAsync();
                        if (isBinary)
                        {
                            var imgDecoder = ImageSource.GetDecoder(_file);
                            if (imgDecoder != Models.ImageDecoder.None)
                            {
                                var source = ImageSource.FromRevision(_repo.FullPath, _revision.SHA, _file, imgDecoder);
                                var image = new Models.RevisionImageFile(_file, source.Bitmap, source.Size);
                                await Dispatcher.UIThread.InvokeAsync(() => ViewContent = new FileHistoriesRevisionFile(_file, image, true));
                            }
                            else
                            {
                                var size = await new Commands.QueryFileSize(_repo.FullPath, _file, _revision.SHA).ResultAsync();
                                var binaryFile = new Models.RevisionBinaryFile() { Size = size };
                                await Dispatcher.UIThread.InvokeAsync(() => ViewContent = new FileHistoriesRevisionFile(_file, binaryFile, true));
                            }

                            return;
                        }

                        var contentStream = await Commands.QueryFileContent.RunAsync(_repo.FullPath, _revision.SHA, _file);
                        var content = await new StreamReader(contentStream).ReadToEndAsync();
                        var lfs = Models.LFSObject.Parse(content);
                        if (lfs != null)
                        {
                            var imgDecoder = ImageSource.GetDecoder(_file);
                            if (imgDecoder != Models.ImageDecoder.None)
                            {
                                var combined = new RevisionLFSImage(_repo.FullPath, _file, lfs, imgDecoder);
                                await Dispatcher.UIThread.InvokeAsync(() => ViewContent = new FileHistoriesRevisionFile(_file, combined, true));
                            }
                            else
                            {
                                var rlfs = new Models.RevisionLFSObject() { Object = lfs };
                                await Dispatcher.UIThread.InvokeAsync(() => ViewContent = new FileHistoriesRevisionFile(_file, rlfs, true));
                            }
                        }
                        else
                        {
                            var txt = new Models.RevisionTextFile() { FileName = obj.Path, Content = content };
                            await Dispatcher.UIThread.InvokeAsync(() => ViewContent = new FileHistoriesRevisionFile(_file, txt, true));
                        }
                    });
                    break;
                case Models.ObjectType.Commit:
                    Task.Run(async () =>
                    {
                        var submoduleRoot = Path.Combine(_repo.FullPath, _file);
                        var commit = await new Commands.QuerySingleCommit(submoduleRoot, obj.SHA).ResultAsync();
                        var message = commit != null ? await new Commands.QueryCommitFullMessage(submoduleRoot, obj.SHA).ResultAsync() : null;
                        var module = new Models.RevisionSubmodule()
                        {
                            Commit = commit ?? new Models.Commit() { SHA = obj.SHA },
                            FullMessage = new Models.CommitFullMessage { Message = message }
                        };

                        await Dispatcher.UIThread.InvokeAsync(() => ViewContent = new FileHistoriesRevisionFile(_file, module));
                    });
                    break;
                default:
                    ViewContent = new FileHistoriesRevisionFile(_file);
                    break;
            }
        }

        private void SetViewContentAsDiff()
        {
            var option = new Models.DiffOption(_revision, _file);
            ViewContent = new DiffContext(_repo.FullPath, option, _viewContent as DiffContext);
        }

        private Repository _repo = null;
        private string _file = null;
        private Models.Commit _revision = null;
        private bool _isDiffMode = false;
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
            return Task.Run(async () =>
            {
                await Commands.SaveChangesAsPatch.ProcessRevisionCompareChangesAsync(_repo.FullPath, _changes, _startPoint.SHA, _endPoint.SHA, saveTo);
                return true;
            });
        }

        private void RefreshViewContent()
        {
            Task.Run(async () =>
            {
                _changes = await new Commands.CompareRevisions(_repo.FullPath, _startPoint.SHA, _endPoint.SHA, _file).ResultAsync();
                if (_changes.Count == 0)
                {
                    await Dispatcher.UIThread.InvokeAsync(() => ViewContent = null);
                    return;
                }

                var option = new Models.DiffOption(_startPoint.SHA, _endPoint.SHA, _changes[0]);
                await Dispatcher.UIThread.InvokeAsync(() => ViewContent = new DiffContext(_repo.FullPath, option, _viewContent));
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
        public string Title
        {
            get;
        }

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
            if (!string.IsNullOrEmpty(commit))
                Title = $"{file} @ {commit}";
            else
                Title = file;

            _repo = repo;

            Task.Run(async () =>
            {
                var based = commit ?? string.Empty;
                var commits = await new Commands.QueryCommits(_repo.FullPath, $"--date-order -n 10000 {based} -- \"{file}\"", false).ResultAsync();
                await Dispatcher.UIThread.InvokeAsync(() =>
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

                ViewContent = SelectedCommits.Count switch
                {
                    1 => new FileHistoriesSingleRevision(_repo, file, SelectedCommits[0], _prevIsDiffMode),
                    2 => new FileHistoriesCompareRevisions(_repo, file, SelectedCommits[0], SelectedCommits[1]),
                    _ => SelectedCommits.Count,
                };
            };
        }

        public void NavigateToCommit(Models.Commit commit)
        {
            _repo.NavigateToCommit(commit.SHA);
        }

        public string GetCommitFullMessage(Models.Commit commit)
        {
            var sha = commit.SHA;
            if (_fullCommitMessages.TryGetValue(sha, out var msg))
                return msg;

            msg = new Commands.QueryCommitFullMessage(_repo.FullPath, sha).Result();
            _fullCommitMessages[sha] = msg;
            return msg;
        }

        private readonly Repository _repo = null;
        private bool _isLoading = true;
        private bool _prevIsDiffMode = true;
        private List<Models.Commit> _commits = null;
        private Dictionary<string, string> _fullCommitMessages = new();
        private object _viewContent = null;
    }
}
