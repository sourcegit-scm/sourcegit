using System.Threading.Tasks;

using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class Blame : ObservableObject
    {
        public string Title
        {
            get;
            private set;
        }

        public string SelectedSHA
        {
            get => _selectedSHA;
            private set => SetProperty(ref _selectedSHA, value);
        }

        public bool IsBinary
        {
            get => _data != null && _data.IsBinary;
        }

        public Models.BlameData Data
        {
            get => _data;
            private set => SetProperty(ref _data, value);
        }

        public Blame(string repo, string file, string revision)
        {
            _repo = repo;

            Title = $"{file} @ {revision.Substring(0, 10)}";
            Task.Run(() =>
            {
                var result = new Commands.Blame(repo, file, revision).Result();
                Dispatcher.UIThread.Invoke(() =>
                {
                    Data = result;
                    OnPropertyChanged(nameof(IsBinary));
                });
            });
        }

        public void NavigateToCommit(string commitSHA)
        {
            var repo = App.FindOpenedRepository(_repo);
            repo?.NavigateToCommit(commitSHA);
        }

        private readonly string _repo = string.Empty;
        private string _selectedSHA = string.Empty;
        private Models.BlameData _data = null;
    }
}
