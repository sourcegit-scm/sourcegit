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
            _repo.SetWatcherEnabled(false);
            ProgressDescription = $"Deleting tag '{Target.Name}' ...";

            var remotes = PushToRemotes ? _repo.Remotes : [];
            var log = _repo.CreateLog("Delete Tag");
            Use(log);

            {
                var succ = await Commands.Tag.DeleteAsync(_repo.FullPath, Target.Name, log);
                if (succ)
                {
                    foreach (var r in remotes)
                        await new Commands.Push(_repo.FullPath, r.Name, $"refs/tags/{Target.Name}", true).Use(log).ExecAsync();
                }

                log.Complete();

                await CallUIThreadAsync(() =>
                {
                    _repo.MarkTagsDirtyManually();
                    _repo.SetWatcherEnabled(true);
                });
                return succ;
            }
        }

        private readonly Repository _repo = null;
    }
}
