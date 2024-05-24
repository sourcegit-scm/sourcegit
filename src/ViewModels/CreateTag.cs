using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class CreateTag : Popup
    {
        public object BasedOn
        {
            get;
            private set;
        }

        [Required(ErrorMessage = "Tag name is required!")]
        [RegularExpression(@"^[\w\-\.]+$", ErrorMessage = "Bad tag name format!")]
        [CustomValidation(typeof(CreateTag), nameof(ValidateTagName))]
        public string TagName
        {
            get => _tagName;
            set => SetProperty(ref _tagName, value, true);
        }

        public string Message
        {
            get;
            set;
        }

        public bool Annotated
        {
            get => _annotated;
            set => SetProperty(ref _annotated, value);
        }

        public bool SignTag
        {
            get;
            set;
        } = false;

        public CreateTag(Repository repo, Models.Branch branch)
        {
            _repo = repo;
            _basedOn = branch.Head;

            BasedOn = branch;
            View = new Views.CreateTag() { DataContext = this };
        }

        public CreateTag(Repository repo, Models.Commit commit)
        {
            _repo = repo;
            _basedOn = commit.SHA;

            BasedOn = commit;
            View = new Views.CreateTag() { DataContext = this };
        }

        public static ValidationResult ValidateTagName(string name, ValidationContext ctx)
        {
            var creator = ctx.ObjectInstance as CreateTag;
            if (creator != null)
            {
                var found = creator._repo.Tags.Find(x => x.Name == name);
                if (found != null)
                    return new ValidationResult("A tag with same name already exists!");
            }
            return ValidationResult.Success;
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Create tag...";

            return Task.Run(() =>
            {
                if (_annotated)
                    Commands.Tag.Add(_repo.FullPath, _tagName, _basedOn, Message, SignTag);
                else
                    Commands.Tag.Add(_repo.FullPath, _tagName, _basedOn);

                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return true;
            });
        }

        private readonly Repository _repo = null;
        private string _tagName = string.Empty;
        private bool _annotated = true;
        private readonly string _basedOn = string.Empty;
    }
}
