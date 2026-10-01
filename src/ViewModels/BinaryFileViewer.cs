using System.IO;
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

        public Models.BinaryFile Content
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
            if (_revision != null)
            {
                string saveTo = Path.GetTempFileName();
                await Commands.SaveRevisionFile.RunAsync(_repo, _revision, _file, saveTo);

                Content = new Models.BinaryFile(saveTo, true);
            }
            else
            {
                Content = new Models.BinaryFile(Path.Combine(_repo, _file), false);
            }

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
        private Models.BinaryFile _content = null;
    }
}
