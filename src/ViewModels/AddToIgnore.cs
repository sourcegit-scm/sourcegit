using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class AddToIgnore : Popup
    {
        [Required(ErrorMessage = "Ignore pattern is required!")]
        public string Pattern
        {
            get => _pattern;
            set => SetProperty(ref _pattern, value, true);
        }

        [Required(ErrorMessage = "Storage file is required!!!")]
        public Models.GitIgnoreFile StorageFile
        {
            get;
            set;
        }

        public AddToIgnore(Repository repo, string pattern)
        {
            _repo = repo;
            _pattern = pattern;
            StorageFile = Models.GitIgnoreFile.Supported[0];
        }

        public override async Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Adding Ignored File(s) ...";

            var file = StorageFile.GetFullPath(_repo.FullPath, _repo.GitDir);
            if (!File.Exists(file))
            {
                await File.WriteAllLinesAsync(file!, [_pattern]);
            }
            else
            {
                var org = await File.ReadAllTextAsync(file);
                if (!org.EndsWith('\n'))
                    await File.AppendAllLinesAsync(file, ["", _pattern]);
                else
                    await File.AppendAllLinesAsync(file, [_pattern]);
            }

            _repo.MarkWorkingCopyDirtyManually();
            _repo.SetWatcherEnabled(true);
            return true;
        }

        private readonly Repository _repo;
        private string _pattern;
    }
}
