using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class BinaryFileViewerStandalone : ObservableObject
    {
        public string FilePath
        {
            get;
            private set;
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public BinaryFile File
        {
            get => _file;
            private set => SetProperty(ref _file, value);
        }

        public BinaryFileViewerStandalone(string repo, string file, string revision)
        {
            FilePath = file;

            Task.Run(async () =>
            {
                var loaded = await BinaryFile.LoadAsync(repo, file, revision).ConfigureAwait(false);

                Dispatcher.UIThread.Post(() =>
                {
                    File = loaded;
                    IsLoading = false;
                });
            });
        }

        private bool _isLoading = true;
        private BinaryFile _file = null;
    }
}
