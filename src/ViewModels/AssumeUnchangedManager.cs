using Avalonia.Collections;
using Avalonia.Threading;
using System.Threading.Tasks;

namespace SourceGit.ViewModels {
    public class AssumeUnchangedManager {
        public AvaloniaList<string> Files { get; private set; }

        public AssumeUnchangedManager(string repo) {
            _repo = repo;
            Files = new AvaloniaList<string>();

            Task.Run(() => {
                var collect = new Commands.AssumeUnchanged(_repo).View();
                Dispatcher.UIThread.Invoke(() => {
                    Files.AddRange(collect);
                });
            });
        }

        public void Remove(object param) {
            if (param is string file) {
                new Commands.AssumeUnchanged(_repo).Remove(file);
                Files.Remove(file);
            }
        }

        private string _repo;
    }
}
