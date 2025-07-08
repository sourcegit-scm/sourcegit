using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class DeinitSubmodule(Repository repo, string submodule) : Popup
    {
        public string Submodule
        {
            get;
        } = submodule;

        public bool Force
        {
            get;
            set;
        }

        public override async Task<bool> Sure()
        {
            repo.SetWatcherEnabled(false);
            ProgressDescription = "De-initialize Submodule";

            var log = repo.CreateLog("De-initialize Submodule");
            Use(log);

            var succ = await new Commands.Submodule(repo.FullPath)
                .Use(log)
                .DeinitAsync(Submodule, false);

            log.Complete();
            repo.SetWatcherEnabled(true);
            return succ;
        }
    }
}
