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
        [RegularExpression(@"^(?!\.)(?!/)(?!.*\.$)(?!.*/$)(?!.*\.\.)[\w\-\./]+$", ErrorMessage = "Bad tag name format!")]
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

        public bool PushToRemotes
        {
            get => _repo.Settings.PushToRemoteWhenCreateTag;
            set => _repo.Settings.PushToRemoteWhenCreateTag = value;
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
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Create tag...";

            var remotes = PushToRemotes ? _repo.Remotes : null;
            var log = _repo.CreateLog("Create Tag");
            Use(log);

            var cmd = new Commands.Tag(_repo.FullPath, _tagName).Use(log);
            var succ = false;
            if (_annotated)
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
            _repo.SetWatcherEnabled(true);
            return succ;
        }

        private readonly Repository _repo = null;
        private string _tagName = string.Empty;
        private bool _annotated = true;
        private readonly string _basedOn;
    }
}
