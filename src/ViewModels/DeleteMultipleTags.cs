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
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Deleting multiple tags...";

            var log = _repo.CreateLog("Delete Multiple Tags");
            Use(log);

            foreach (var tag in Tags)
            {
                var succ = await new Commands.Tag(_repo.FullPath, tag.Name)
                .Use(log)
                .DeleteAsync();

                if (succ)
                {
                    foreach (var r in _repo.Remotes)
                        await new Commands.Push(_repo.FullPath, r.Name, $"refs/tags/{tag.Name}", true)
                            .Use(log)
                            .RunAsync();
                }
            }

            log.Complete();
            _repo.MarkTagsDirtyManually();
            _repo.SetWatcherEnabled(true);
            return true;
        }

        private readonly Repository _repo;
    }
}
