using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Threading.Tasks;

namespace SourceGit.ViewModels {
    public class Statistics : ObservableObject {
        public bool IsLoading {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }
        
        public Models.Statistics Data {
            get => _data;
            private set => SetProperty(ref _data, value);
        }

        public Statistics(string repo) {
            _repo = repo;

            Task.Run(() => {
                var result = new Commands.Statistics(_repo).Result();
                Dispatcher.UIThread.Invoke(() => {
                    IsLoading = false;
                    Data = result;
                });
            });
        }

        private string _repo = string.Empty;
        private bool _isLoading = true;
        private Models.Statistics _data = null;
    }
}
