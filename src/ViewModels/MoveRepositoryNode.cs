using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class MoveRepositoryNode : Popup
    {
        public IReadOnlyList<RepositoryNode> Targets
        {
            get;
        }

        public string TargetsDescription
        {
            get;
        }

        public List<RepositoryNode> Rows
        {
            get;
        } = [];

        public RepositoryNode Selected
        {
            get => _selected;
            set => SetProperty(ref _selected, value);
        }

        public MoveRepositoryNode(RepositoryNode target) : this([target])
        {
        }

        public MoveRepositoryNode(IReadOnlyList<RepositoryNode> targets)
        {
            Targets = targets;
            TargetsDescription = targets.Count == 1 ? targets[0].Name : App.Text("MoveRepositoryNode.Items", targets.Count);

            var scopeRoot = Preferences.Instance.GetScopeRoot();
            Rows.Add(new RepositoryNode()
            {
                Name = scopeRoot?.Name ?? "ROOT",
                Depth = 0,
                Id = scopeRoot?.Id ?? Guid.NewGuid().ToString()
            });
            MakeRows(scopeRoot?.SubNodes ?? Preferences.Instance.RepositoryNodes, 1);
            Selected = Rows[0];
        }

        public override Task<bool> Sure()
        {
            if (_selected != null)
            {
                var node = Preferences.Instance.FindNode(_selected.Id);
                Welcome.Instance.MoveNodes(Targets, node);
            }

            return Task.FromResult(true);
        }

        private void MakeRows(List<RepositoryNode> collection, int depth)
        {
            foreach (var node in collection)
            {
                if (node.IsRepository || IsTarget(node))
                    continue;

                var dump = new RepositoryNode()
                {
                    Name = node.Name,
                    Depth = depth,
                    Id = node.Id
                };
                Rows.Add(dump);
                MakeRows(node.SubNodes, depth + 1);
            }
        }

        private bool IsTarget(RepositoryNode node)
        {
            foreach (var target in Targets)
            {
                if (target.Id == node.Id)
                    return true;
            }

            return false;
        }

        private RepositoryNode _selected = null;
    }
}
