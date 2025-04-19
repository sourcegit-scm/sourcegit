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

            return null;
        }

        private RepositoryNode _node = null;
        private string _id = null;
        private string _name = null;
        private bool _isRepository = false;
        private int _bookmark = 0;
    }
}
