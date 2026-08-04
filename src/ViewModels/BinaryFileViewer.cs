using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class BinaryFileViewer : ObservableObject
    {
        public string File
        {
            get => _file;
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public BinaryFile Content
        {
            get => _content;
            private set => SetProperty(ref _content, value);
        }

        public BinaryFileViewer(string repo, string file, string revision)
        {
            _repo = repo;
            _file = file;
            _revision = revision;
        }

        public async Task LoadAsync()
        {
            Content = await BinaryFile.LoadAsync(_repo, _file, _revision)
                .ConfigureAwait(false);
            IsLoading = false;
        }

        public void Cleanup()
        {
            _repo = null;
            _file = null;
            _revision = null;

            _content?.Dispose();
            _content = null;
        }

        private bool _isLoading = true;
        private string _repo = null;
        private string _file = null;
        private string _revision = null;
        private BinaryFile _content = null;
    }
}
