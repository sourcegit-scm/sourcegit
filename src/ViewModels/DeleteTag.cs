using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class DeleteTag : Popup
    {
        public Models.Tag Target
        {
            get;
            private set;
        }

        public bool PushToRemotes
        {
            get => _repo.Settings.PushToRemoteWhenDeleteTag;
            set => _repo.Settings.PushToRemoteWhenDeleteTag = value;
        }

        public DeleteTag(Repository repo, Models.Tag tag)
        {
            _repo = repo;
            Target = tag;
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = $"Deleting tag '{Target.Name}' ...";

            var remotes = PushToRemotes ? _repo.Remotes : [];
            var log = _repo.CreateLog("Delete Tag");
            Use(log);

            var succ = await new Commands.Tag(_repo.FullPath, Target.Name)
                .Use(log)
                .DeleteAsync();

            if (succ)
            {
                foreach (var r in remotes)
                    await new Commands.Push(_repo.FullPath, r.Name, $"refs/tags/{Target.Name}", true)
                        .Use(log)
                        .RunAsync();
            }

            log.Complete();
            _repo.MarkTagsDirtyManually();
            return succ;
        }

        private readonly Repository _repo = null;
    }
}
