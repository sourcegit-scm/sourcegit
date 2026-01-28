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
        }

        public override async Task<bool> Sure()
        {
            ProgressDescription = $"Initialize git repository at: '{_targetPath}'";

            var log = new CommandLog("Initialize");
            Use(log);

            var succ = await new Commands.Init(_pageId, _targetPath)
                .Use(log)
                .ExecAsync();

            log.Complete();

            if (succ)
            {
                var node = Preferences.Instance.FindOrAddNodeByRepositoryPath(_targetPath, _parentNode, true);
                await node.UpdateStatusAsync(false, null);

                Welcome.Instance.Refresh();
            }
            return succ;
        }

        private readonly string _pageId = null;
        private string _targetPath = null;
        private readonly RepositoryNode _parentNode = null;
    }
}
