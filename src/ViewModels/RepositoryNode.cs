using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class RepositoryNode : ObservableObject
    {
        public string Id
        {
            get => _id;
            set
            {
                var normalized = value.Replace('\\', '/').TrimEnd('/');
                SetProperty(ref _id, normalized);
            }
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
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

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        [JsonIgnore]
        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        [JsonIgnore]
        public bool IsInvalid
        {
            get => _isRepository && !Directory.Exists(_id);
        }

        [JsonIgnore]
        public int Depth
        {
            get;
            set;
        } = 0;

        public List<RepositoryNode> SubNodes
        {
            get;
            set;
        } = [];

        public void Open()
        {
            if (IsRepository)
            {
                App.GetLauncher().OpenRepositoryInTab(this, null);
                return;
            }

            foreach (var subNode in SubNodes)
                subNode.Open();
        }

        public void Edit()
        {
            var activePage = App.GetLauncher().ActivePage;
            if (activePage != null && activePage.CanCreatePopup())
                activePage.Popup = new EditRepositoryNode(this);
        }

        public void AddSubFolder()
        {
            var activePage = App.GetLauncher().ActivePage;
            if (activePage != null && activePage.CanCreatePopup())
                activePage.Popup = new CreateGroup(this);
        }

        public void Move()
        {
            var activePage = App.GetLauncher().ActivePage;
            if (activePage != null && activePage.CanCreatePopup())
                activePage.Popup = new MoveRepositoryNode(this);
        }

        public void OpenInFileManager()
        {
            if (!IsRepository)
                return;
            Native.OS.OpenInFileManager(_id);
        }

        public void OpenTerminal()
        {
            if (!IsRepository)
                return;
            Native.OS.OpenTerminal(_id);
        }

        public void Delete()
        {
            var activePage = App.GetLauncher().ActivePage;
            if (activePage != null && activePage.CanCreatePopup())
                activePage.Popup = new DeleteRepositoryNode(this);
        }

        private string _id = string.Empty;
        private string _name = string.Empty;
        private bool _isRepository = false;
        private int _bookmark = 0;
        private bool _isExpanded = false;
        private bool _isVisible = true;
    }
}
