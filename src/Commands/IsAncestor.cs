using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class IsAncestor : Command
    {
        public IsAncestor(string repo, string ancestor, string descendant)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"merge-base --is-ancestor {ancestor} {descendant}";
            RaiseError = false;
        }

        public bool Test()
        {
            return ReadToEnd().IsSuccess;
        }
    }
}
