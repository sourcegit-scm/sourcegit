using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class EditRepositoryNode : Popup
    {
        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        [Required(ErrorMessage = "Name is required!")]
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value, true);
        }

        public int Bookmark
        {
            get => _bookmark;
            set => SetProperty(ref _bookmark, value);
        }

        public bool IsRepository
        {
            get => _isRepository;
            set => SetProperty(ref _isRepository, value);
        }

        public EditRepositoryNode(RepositoryNode node)
        {
            _node = node;
            _id = node.Id;
            _name = node.Name;
            _isRepository = node.IsRepository;
            _bookmark = node.Bookmark;
        }

        public override Task<bool> Sure()
        {
            bool needSort = _node.Name != _name;
            _node.Name = _name;
            _node.Bookmark = _bookmark;

            if (needSort)
            {
                Preferences.Instance.SortByRenamedNode(_node);
                Welcome.Instance.Refresh();
            }

            return Task.FromResult(true);
        }

        private readonly RepositoryNode _node;
        private string _id;
        private string _name;
        private bool _isRepository;
        private int _bookmark;
    }
}
