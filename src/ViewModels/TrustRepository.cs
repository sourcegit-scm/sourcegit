using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class TrustRepository : Popup
    {
        public string TargetPath
        {
            get;
        }

        public string SafeDirectory
        {
            get;
        }

        public string Command
        {
            get;
        }

        public string Reason
        {
            get;
        }

        public TrustRepository(string pageId, string path, string reason, string safeDirectory, RepositoryNode parent, bool moveNode, bool open, int bookmark)
        {
            _pageId = pageId;
            _parent = parent;
            _moveNode = moveNode;
            _open = open;
            _bookmark = bookmark;

            TargetPath = path;
            SafeDirectory = safeDirectory;
            Command = App.Text("TrustRepository.CommandTip", safeDirectory);
            Reason = GetReason(reason);
        }

        public override async Task<bool> Sure()
        {
            var log = new CommandLog("Trust Repository");
            Use(log);

            ProgressDescription = $"Adding '{SafeDirectory}' into git global `safe.directory` ...";
            var added = await new Commands.AddSafeDirectory(_pageId, SafeDirectory).Use(log).ExecAsync();
            if (!added)
            {
                log.Complete();
                return false;
            }

            ProgressDescription = $"Opening '{TargetPath}' ...";

            var root = TargetPath;
            var isBare = await new Commands.IsBareRepository(TargetPath).GetResultAsync();
            if (!isBare)
            {
                var test = await new Commands.QueryRepositoryRootPath(TargetPath).GetResultAsync();
                if (!test.IsSuccess || string.IsNullOrWhiteSpace(test.StdOut))
                {
                    log.Complete();
                    Models.Notification.Send(_pageId, string.IsNullOrWhiteSpace(test.StdErr) ? "Failed to open the repository after trusting it." : test.StdErr, true);
                    return false;
                }

                root = test.StdOut.Trim();
            }

            log.Complete();

            var node = Preferences.Instance.FindOrAddNodeByRepositoryPath(root, _parent, _moveNode);
            node.Bookmark = _bookmark;
            await node.UpdateStatusAsync(false, null);
            Welcome.Instance.Refresh();

            if (_open)
                node.Open();

            return true;
        }

        private static string GetReason(string stderr)
        {
            if (string.IsNullOrWhiteSpace(stderr))
                return string.Empty;

            // Only the first line (e.g. "fatal: detected dubious ownership in repository at '...'") is shown.
            // The remaining lines only contain the owner SIDs and git's suggested `git config` command.
            var line = stderr.Trim();
            var idx = line.IndexOf('\n');
            if (idx >= 0)
                line = line.Substring(0, idx);

            return line.Trim();
        }

        private readonly string _pageId;
        private readonly RepositoryNode _parent;
        private readonly bool _moveNode;
        private readonly bool _open;
        private readonly int _bookmark;
    }
}
