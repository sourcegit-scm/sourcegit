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
            var launcher = App.GetLauncer();
            if (launcher == null)
                return;

            foreach (var page in launcher.Pages)
            {
                if (page.Data is Repository repo && repo.FullPath.Equals(_repo))
                {
                    repo.NavigateToCommit(commitSHA);
                    break;
                }
            }
        }

        private readonly string _repo;
        private Models.BlameData _data = null;
    }
}
