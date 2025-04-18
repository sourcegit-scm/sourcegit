using System.Threading.Tasks;

using Avalonia.Collections;
using Avalonia.Threading;

namespace SourceGit.ViewModels
{
    public class AssumeUnchangedManager
    {
        public AvaloniaList<string> Files { get; private set; }

        public AssumeUnchangedManager(Repository repo)
        {
            _repo = repo;
            Files = new AvaloniaList<string>();

            Task.Run(() =>
            {
                var collect = new Commands.QueryAssumeUnchangedFiles(_repo.FullPath).Result();
                Dispatcher.UIThread.Invoke(() => Files.AddRange(collect));
            });
        }

        public void Remove(string file)
        {
            if (!string.IsNullOrEmpty(file))
            {
                var log = _repo.CreateLog("Remove Assue Unchanged File");
                new Commands.AssumeUnchanged(_repo.FullPath, file, false).Use(log).Exec();
                log.Complete();
                Files.Remove(file);
            }
        }

        private readonly Repository _repo;
    }
}
