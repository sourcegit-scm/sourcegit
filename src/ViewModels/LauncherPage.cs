using System;

using Avalonia.Collections;

namespace SourceGit.ViewModels
{
    public class LauncherPage : PopupHost
    {
        public RepositoryNode Node
        {
            get => _node;
            set => SetProperty(ref _node, value);
        }

        public object Data
        {
            get => _data;
            set => SetProperty(ref _data, value);
        }

        public bool IsTabSplitterVisible
        {
            get => _isTabSplitterVisible;
            set => SetProperty(ref _isTabSplitterVisible, value);
        }

        public AvaloniaList<Notification> Notifications
        {
            get;
            set;
        } = new AvaloniaList<Notification>();

        public LauncherPage()
        {
            _node = new RepositoryNode() { Id = Guid.NewGuid().ToString() };
            _data = Welcome.Instance;
        }

        public LauncherPage(RepositoryNode node, Repository repo)
        {
            _node = node;
            _data = repo;
        }

        public override string GetId()
        {
            return _node.Id;
        }

        public void CopyPath()
        {
            if (_node.IsRepository)
                App.CopyText(_node.Id);
        }

        private RepositoryNode _node = null;
        private object _data = null;
        private bool _isTabSplitterVisible = true;
    }
}
