using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class Init : Popup
    {
        public string TargetPath
        {
            get => _targetPath;
            set => SetProperty(ref _targetPath, value);
        }

        public string Reason
        {
            get;
            private set;
        }

        public Init(string pageId, string path, RepositoryNode parent, string reason)
        {
            _pageId = pageId;
            _targetPath = path;
            _parentNode = parent;

            Reason = string.IsNullOrEmpty(reason) ? "Invalid repository detected!" : reason;
            View = new Views.Init() { DataContext = this };
        }

        public override Task<bool> Sure()
        {
            ProgressDescription = $"Initialize git repository at: '{_targetPath}'";

            return Task.Run(() =>
            {
                var succ = new Commands.Init(_pageId, _targetPath).Exec();
                if (!succ)
                    return false;

                CallUIThread(() =>
                {
                    var normalizedPath = _targetPath.Replace("\\", "/");
                    Preference.Instance.FindOrAddNodeByRepositoryPath(normalizedPath, _parentNode, true);
                    Welcome.Instance.Refresh();
                });

                return true;
            });
        }

        private string _pageId = null;
        private string _targetPath = null;
        private RepositoryNode _parentNode = null;
    }
}
