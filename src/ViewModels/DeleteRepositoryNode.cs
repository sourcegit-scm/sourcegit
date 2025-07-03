using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class DeleteRepositoryNode : Popup
    {
        public RepositoryNode Node
        {
            get;
        }

        public DeleteRepositoryNode(RepositoryNode node)
        {
            Node = node;
        }

        public override Task<bool> Sure()
        {
            Preferences.Instance.RemoveNode(Node, true);
            Welcome.Instance.Refresh();
            return Task.FromResult(true);
        }
    }
}
