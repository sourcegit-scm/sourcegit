using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public static class GitFlow
    {
        public static async Task<bool> InitAsync(string repo, string master, string develop, string feature, string release, string hotfix, string version, Models.ICommandLog log)
        {
            var config = new Config(repo);
            await config.SetAsync("gitflow.branch.master", master);
            await config.SetAsync("gitflow.branch.develop", develop);
            await config.SetAsync("gitflow.prefix.feature", feature);
            await config.SetAsync("gitflow.prefix.bugfix", "bugfix/");
            await config.SetAsync("gitflow.prefix.release", release);
            await config.SetAsync("gitflow.prefix.hotfix", hotfix);
            await config.SetAsync("gitflow.prefix.support", "support/");
            await config.SetAsync("gitflow.prefix.versiontag", version, true);

            var init = new Command();
            init.WorkingDirectory = repo;
            init.Context = repo;
            init.Args = "flow init -d";
            init.Log = log;
            return await init.ExecAsync();
        }

        public static async Task<bool> StartAsync(string repo, Models.GitFlowBranchType type, string name, Models.ICommandLog log)
        {
            var start = new Command();
            start.WorkingDirectory = repo;
            start.Context = repo;

            switch (type)
            {
                case Models.GitFlowBranchType.Feature:
                    start.Args = $"flow feature start {name}";
                    break;
                case Models.GitFlowBranchType.Release:
                    start.Args = $"flow release start {name}";
                    break;
                case Models.GitFlowBranchType.Hotfix:
                    start.Args = $"flow hotfix start {name}";
                    break;
                default:
                    App.RaiseException(repo, "Bad git-flow branch type!!!");
                    return false;
            }

            start.Log = log;
            return await start.ExecAsync();
        }

        public static async Task<bool> FinishAsync(string repo, Models.GitFlowBranchType type, string name, bool squash, bool push, bool keepBranch, Models.ICommandLog log)
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
                    App.RaiseException(repo, "Bad git-flow branch type!!!");
                    return false;
            }

            builder.Append(" finish ");
            if (squash)
                builder.Append("--squash ");
            if (push)
                builder.Append("--push ");
            if (keepBranch)
                builder.Append("-k ");
            builder.Append(name);

            var finish = new Command();
            finish.WorkingDirectory = repo;
            finish.Context = repo;
            finish.Args = builder.ToString();
            finish.Log = log;
            return await finish.ExecAsync();
        }
    }
}
