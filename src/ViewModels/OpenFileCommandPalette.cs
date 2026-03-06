using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace SourceGit.ViewModels
{
    public class OpenFileCommandPalette : ICommandPalette
    {
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public List<string> VisibleFiles
        {
            get => _visibleFiles;
            private set => SetProperty(ref _visibleFiles, value);
        }

        public string Filter
        {
            get => _filter;
            set
            {
                if (SetProperty(ref _filter, value))
                    UpdateVisible();
            }
        }

        public string SelectedFile
        {
            get => _selectedFile;
            set => SetProperty(ref _selectedFile, value);
        }

        public OpenFileCommandPalette(string repo)
        {
            _repo = repo;
            _isLoading = true;

            Task.Run(async () =>
            {
                var files = await new Commands.QueryRevisionFileNames(_repo, "HEAD")
                    .GetResultAsync()
                    .ConfigureAwait(false);

                Dispatcher.UIThread.Post(() =>
                {
                    IsLoading = false;
                    _repoFiles = files;
                    UpdateVisible();
                });
            });
        }

        public void ClearFilter()
        {
            Filter = string.Empty;
        }

        public void Launch()
        {
            _repoFiles.Clear();
            _visibleFiles.Clear();
            Close();

            if (!string.IsNullOrEmpty(_selectedFile))
                Native.OS.OpenWithDefaultEditor(Native.OS.GetAbsPath(_repo, _selectedFile));
        }

        private void UpdateVisible()
        {
            if (_repoFiles is { Count: > 0 })
            {
                if (string.IsNullOrEmpty(_filter))
                {
                    VisibleFiles = _repoFiles;

                    if (string.IsNullOrEmpty(_selectedFile))
                        SelectedFile = _repoFiles[0];
                }
                else
                {
                    var visible = new List<string>();

                    foreach (var f in _repoFiles)
                    {
                        if (f.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                            visible.Add(f);
                    }

                    var autoSelected = _selectedFile;
                    if (visible.Count == 0)
                        autoSelected = null;
                    else if (string.IsNullOrEmpty(_selectedFile) || !visible.Contains(_selectedFile))
                        autoSelected = visible[0];

                    VisibleFiles = visible;
                    SelectedFile = autoSelected;
                }
            }
        }

        private string _repo = null;
        private bool _isLoading = false;
        private List<string> _repoFiles = null;
        private string _filter = string.Empty;
        private List<string> _visibleFiles = [];
        private string _selectedFile = null;
    }
}
