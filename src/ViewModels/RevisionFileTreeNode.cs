using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class RevisionFileTreeNode : ObservableObject
    {
        public Models.Object Backend
        {
            get => _compacted?.Backend ?? _backend;
            set => _backend = value;
        }

        public int Depth { get; set; } = 0;
        public List<RevisionFileTreeNode> Children => _compacted?.Children ?? _children;
        public string Name => _backend == null ? string.Empty : Path.GetFileName(_backend.Path) + (_compacted == null ? string.Empty : " / " + _compacted.Name);
        public bool IsFolder => _backend?.Type == Models.ObjectType.Tree;

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _hasExpansionState = true;
                SetProperty(ref _isExpanded, value);
                if (_compacted != null)
                    _compacted.IsExpanded = value;
            }
        }

        // Keep the original hierarchy: switching compaction off restores its nodes,
        // cached children and expansion state without changing any Git object paths.
        public async Task PrepareAsync(Func<string, Task<List<Models.Object>>> loadChildren, bool compactFolders)
        {
            RevisionFileTreeNode compacted = null;
            if (IsFolder && compactFolders)
            {
                await LoadOwnChildrenAsync(loadChildren);
                if (_children.Count == 1 && _children[0].IsFolder)
                {
                    var child = _children[0];
                    await child.PrepareAsync(loadChildren, true);
                    compacted = child;
                    if (child._hasExpansionState && !child.IsExpanded)
                        IsExpanded = false;
                    child.IsExpanded = IsExpanded;
                }
            }

            _compacted = compacted;
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Backend));
        }

        public async Task<List<RevisionFileTreeNode>> LoadChildrenAsync(Func<string, Task<List<Models.Object>>> loadChildren)
        {
            if (_compacted != null)
                return await _compacted.LoadChildrenAsync(loadChildren);
            return await LoadOwnChildrenAsync(loadChildren);
        }

        private async Task<List<RevisionFileTreeNode>> LoadOwnChildrenAsync(Func<string, Task<List<Models.Object>>> loadChildren)
        {
            if (!IsFolder)
                return _children;
            if (_children.Count == 0)
            {
                _loading ??= loadChildren(_backend.Path + "/");
                var objects = await _loading;
                if (_children.Count == 0 && objects != null)
                {
                    foreach (var obj in objects)
                        _children.Add(new RevisionFileTreeNode { Backend = obj });
                }
            }
            return _children;
        }

        private Models.Object _backend;
        private readonly List<RevisionFileTreeNode> _children = [];
        private RevisionFileTreeNode _compacted;
        private Task<List<Models.Object>> _loading;
        private bool _hasExpansionState;
        private bool _isExpanded = false;
    }
}
