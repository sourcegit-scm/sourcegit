using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class GitFlow : Command
    {
        public GitFlow(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
        }

        public async Task<bool> InitAsync(string production, string develop, string feature, string release, string hotfix, string tag)
        {
            if (Native.OS.GitFlowVersion == Models.GitFlowVersion.Next)
            {
                var builder = new StringBuilder();
                builder
                    .Append("flow init --preset=classic ")
                    .Append("--main=").Append(production.Quoted()).Append(' ')
                    .Append("--develop=").Append(develop.Quoted()).Append(' ')
                    .Append("--feature=").Append(feature).Append(' ')
                    .Append("--bugfix=bugfix/ ")
                    .Append("--release=").Append(release).Append(' ')
                    .Append("--hotfix=").Append(hotfix).Append(' ')
                    .Append("--support=support/");

                if (!string.IsNullOrEmpty(tag))
                    builder.Append(" --tag=").Append(tag);

                Args = builder.ToString();
                return await ExecAsync().ConfigureAwait(false);
            }

            var config = new Config(WorkingDirectory);
            await config.SetAsync("gitflow.branch.master", production).ConfigureAwait(false);
            await config.SetAsync("gitflow.branch.develop", develop).ConfigureAwait(false);
            await config.SetAsync("gitflow.prefix.feature", feature).ConfigureAwait(false);
            await config.SetAsync("gitflow.prefix.bugfix", "bugfix/").ConfigureAwait(false);
            await config.SetAsync("gitflow.prefix.release", release).ConfigureAwait(false);
            await config.SetAsync("gitflow.prefix.hotfix", hotfix).ConfigureAwait(false);
            await config.SetAsync("gitflow.prefix.support", "support/").ConfigureAwait(false);
            await config.SetAsync("gitflow.prefix.versiontag", tag, true).ConfigureAwait(false);

            Args = "flow init -d";
            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task<bool> StartAsync(Models.GitFlowBranchType type, string name)
        {
            switch (type)
            {
                case Models.GitFlowBranchType.Feature:
                    Args = $"flow feature start {name}";
                    break;
                case Models.GitFlowBranchType.Release:
                    Args = $"flow release start {name}";
                    break;
                case Models.GitFlowBranchType.Hotfix:
                    Args = $"flow hotfix start {name}";
                    break;
                default:
                    RaiseException("Bad git-flow branch type!!!");
                    return false;
            }

            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task<bool> FinishAsync(Models.GitFlowBranchType type, string name, bool rebase, bool squash, bool keepBranch)
        {
            var builder = new StringBuilder();
            builder.Append("flow ");

            switch (type)
            {
                case Models.GitFlowBranchType.Feature:
                    builder.Append("feature");
                    break;
                case Models.GitFlowBranchType.Release:
                    builder.Append("release");
                    break;
                case Models.GitFlowBranchType.Hotfix:
                    builder.Append("hotfix");
                    break;
                default:
                    RaiseException("Bad git-flow branch type!!!");
                    return false;
            }

            builder.Append(" finish ");
            if (rebase)
                builder.Append("--rebase ");
            if (squash)
                builder.Append("--squash ");
            if (keepBranch)
                builder.Append("--keep ");
            builder.Append(name);

            Args = builder.ToString();
            return await ExecAsync().ConfigureAwait(false);
        }
    }
}
