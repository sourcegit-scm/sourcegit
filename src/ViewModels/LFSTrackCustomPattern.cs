using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class LFSTrackCustomPattern : Popup
    {
        [Required(ErrorMessage = "LFS track pattern is required!!!")]
        public string Pattern
        {
            get => _pattern;
            set => SetProperty(ref _pattern, value, true);
        }

        public bool IsFilename
        {
            get;
            set;
        } = false;

        public LFSTrackCustomPattern(Repository repo)
        {
            _repo = repo;
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Adding custom LFS tracking pattern ...";

            var log = _repo.CreateLog("LFS Add Custom Pattern");
            Use(log);

            return Task.Run(async () =>
            {
                var succ = await new Commands.LFS(_repo.FullPath).TrackAsync(_pattern, IsFilename, log);
                log.Complete();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private readonly Repository _repo = null;
        private string _pattern = string.Empty;
    }
}
