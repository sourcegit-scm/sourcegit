using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class DoesBranchExistOnRemote : Command
    {
        public DoesBranchExistOnRemote(string repo, Models.Remote remote, Models.Branch branch)
        {
            WorkingDirectory = repo;
            SSHKey = remote.PrivateSSHKey;
            RaiseError = false;
            Args = $"ls-remote {remote.Name} {branch.Name}";
        }

        public async Task<bool> GetResultAsync()
        {
            var rs = await ReadToEndAsync();
            return rs.IsSuccess && rs.StdOut.Trim().Length > 0;
        }
    }
}
