using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class MoveRepositoryNode : Popup
    {
        public RepositoryNode Target
        {
            get;
        } = null;

        public List<RepositoryNode> Rows
        {
            get;
        } = [];

        public RepositoryNode Selected
        {
            get => _selected;
            set => SetProperty(ref _selected, value);
        }

        public MoveRepositoryNode(RepositoryNode target)
        {
            Target = target;
            Rows.Add(new RepositoryNode()
            {
                Name = "ROOT",
                Depth = 0,
                Id = Guid.NewGuid().ToString()
            });
            MakeRows(Preferences.Instance.RepositoryNodes, 1);
        }

        public override Task<bool> Sure()
        {
            if (_selected != null)
            {
                var node = Preferences.Instance.FindNode(_selected.Id);
                Preferences.Instance.MoveNode(Target, node, true);
                Welcome.Instance.Refresh();
            }

            return Task.FromResult(true);
        }

        private void MakeRows(List<RepositoryNode> collection, int depth)
        {
            foreach (var node in collection)
            {
                if (node.IsRepository || node.Id == Target.Id)
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

        private RepositoryNode _selected = null;
    }
}
