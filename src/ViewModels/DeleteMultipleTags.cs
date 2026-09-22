using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class DeleteMultipleTags : Popup
    {
        public List<Models.Tag> Tags
        {
            get;
        }

        public bool DeleteFromRemote
        {
            get;
            set;
        } = false;

        public DeleteMultipleTags(Repository repo, List<Models.Tag> tags)
        {
            _repo = repo;
            Tags = tags;
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Deleting multiple tags...";

            var log = _repo.CreateLog("Delete Multiple Tags");
            Use(log);

            foreach (var tag in Tags)
            {
                var succ = await new Commands.Tag(_repo.FullPath, tag.Name)
                .Use(log)
                .DeleteAsync();

                if (succ && DeleteFromRemote && _repo.Remotes is { Count: > 0 } remotes)
                {
                    var fullname = $"refs/tags/{tag.Name}";
                    foreach (var r in remotes)
                        await new Commands.Push(_repo.FullPath, r, fullname, true)
                            .Use(log)
                            .ExecAsync();
                }
            }

            log.Complete();
            _repo.MarkTagsDirtyManually();
            return true;
        }

        private readonly Repository _repo;
    }
}
