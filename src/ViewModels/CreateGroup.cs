using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class CreateGroup : Popup
    {
        [Required(ErrorMessage = "Group name is required!")]
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value, true);
        }

        public CreateGroup(RepositoryNode parent)
        {
            _parent = parent;
        }

        public override Task<bool> Sure()
        {
            Preferences.Instance.AddNode(new RepositoryNode()
            {
                Id = Guid.NewGuid().ToString(),
                Name = _name,
                IsRepository = false,
                IsExpanded = false,
            }, _parent, true);

            Welcome.Instance.Refresh();
            return Task.FromResult(true);
        }

        private readonly RepositoryNode _parent = null;
        private string _name = string.Empty;
    }
}
