using System;
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
        [RegularExpression(@"^(?!\.)(?!/)(?!.*\.$)(?!.*/$)(?!.*\.\.)[\w\-\+\./]+$", ErrorMessage = "Bad tag name format!")]
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
            get => _repo.UIStates.CreateAnnotatedTag;
            set
            {
                if (_repo.UIStates.CreateAnnotatedTag != value)
                {
                    _repo.UIStates.CreateAnnotatedTag = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool SignTag
        {
            get;
            set;
        } = false;

        public bool PushToRemotes
        {
            get => _repo.UIStates.PushToRemoteWhenCreateTag;
            set => _repo.UIStates.PushToRemoteWhenCreateTag = value;
        }

        public CreateTag(Repository repo, Models.Branch branch)
        {
            _repo = repo;
            _basedOn = branch.Head;

            BasedOn = branch;
            SignTag = new Commands.Config(repo.FullPath).Get("tag.gpgsign").Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public CreateTag(Repository repo, Models.Commit commit)
        {
            _repo = repo;
            _basedOn = commit.SHA;

            BasedOn = commit;
            SignTag = new Commands.Config(repo.FullPath).Get("tag.gpgsign").Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public static ValidationResult ValidateTagName(string name, ValidationContext ctx)
        {
            if (ctx.ObjectInstance is CreateTag creator)
            {
                var found = creator._repo.Tags.Find(x => x.Name == name);
                if (found != null)
                    return new ValidationResult("A tag with same name already exists!");
            }
            return ValidationResult.Success;
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Create tag...";

            var remotes = PushToRemotes ? _repo.Remotes : null;
            var log = _repo.CreateLog("Create Tag");
            Use(log);

            var cmd = new Commands.Tag(_repo.FullPath, _tagName)
                .Use(log);

            bool succ;
            if (_repo.UIStates.CreateAnnotatedTag)
                succ = await cmd.AddAsync(_basedOn, Message, SignTag);
            else
                succ = await cmd.AddAsync(_basedOn);

            if (succ && remotes != null)
            {
                foreach (var remote in remotes)
                    await new Commands.Push(_repo.FullPath, remote.Name, $"refs/tags/{_tagName}", false)
                        .Use(log)
                        .RunAsync();
            }

            log.Complete();
            return succ;
        }

        private readonly Repository _repo = null;
        private string _tagName = string.Empty;
        private readonly string _basedOn;
    }
}
