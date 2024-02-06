using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

namespace SourceGit.ViewModels {
    public class Archive : Popup {

        [Required(ErrorMessage = "Output file name is required")]
        public string SaveFile {
            get => _saveFile;
            set => SetProperty(ref _saveFile, value, true);
        }

        public object BasedOn {
            get;
            private set;
        }

        public Archive(Repository repo, Models.Branch branch) {
            _repo = repo;
            _revision = branch.Head;
            _saveFile = $"archive-{Path.GetFileNameWithoutExtension(branch.Name)}.zip";
            BasedOn = branch;
            View = new Views.Archive() { DataContext = this };
        }

        public Archive(Repository repo, Models.Commit commit) {
            _repo = repo;
            _revision = commit.SHA;
            _saveFile = $"archive-{commit.SHA.Substring(0,10)}.zip";
            BasedOn = commit;
            View = new Views.Archive() { DataContext = this };
        }

        public Archive(Repository repo, Models.Tag tag) {
            _repo = repo;
            _revision = tag.SHA;
            _saveFile = $"archive-{tag.Name}.zip";
            BasedOn = tag;
            View = new Views.Archive() { DataContext = this };
        }

        public override Task<bool> Sure() {
            _repo.SetWatcherEnabled(false);
            return Task.Run(() => {
                var succ = new Commands.Archive(_repo.FullPath, _revision, _saveFile, SetProgressDescription).Exec();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private Repository _repo = null;
        private string _saveFile = string.Empty;
        private string _revision = string.Empty;
    }
}
