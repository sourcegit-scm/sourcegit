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
            get => _repo.UIStates.PushToRemoteWhenDeleteTag;
            set => _repo.UIStates.PushToRemoteWhenDeleteTag = value;
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

            var log = _repo.CreateLog("Delete Tag");
            Use(log);

            var succ = await new Commands.Tag(_repo.FullPath, Target.Name)
                .Use(log)
                .DeleteAsync();

            if (succ && PushToRemotes && _repo.Remotes is { Count: > 0 } remotes)
            {
                var fullname = $"refs/tags/{Target.Name}";
                foreach (var r in remotes)
                    await new Commands.Push(_repo.FullPath, r, fullname, true)
                        .Use(log)
                        .ExecAsync();
            }

            log.Complete();
            _repo.UIStates.RemoveHistoryFilter(Target.Name, Models.FilterType.Tag);
            _repo.MarkTagsDirtyManually();
            return succ;
        }

        private readonly Repository _repo = null;
    }
}
