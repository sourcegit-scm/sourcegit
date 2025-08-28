using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public static class GitFlow
    {
        public static async Task<bool> InitAsync(string repo, string master, string develop, string feature, string release, string hotfix, string version, Models.ICommandLog log)
        {
            var config = new Config(repo);
            await config.SetAsync("gitflow.branch.master", master).ConfigureAwait(false);
            await config.SetAsync("gitflow.branch.develop", develop).ConfigureAwait(false);
            await config.SetAsync("gitflow.prefix.feature", feature).ConfigureAwait(false);
            await config.SetAsync("gitflow.prefix.bugfix", "bugfix/").ConfigureAwait(false);
            await config.SetAsync("gitflow.prefix.release", release).ConfigureAwait(false);
            await config.SetAsync("gitflow.prefix.hotfix", hotfix).ConfigureAwait(false);
            await config.SetAsync("gitflow.prefix.support", "support/").ConfigureAwait(false);
            await config.SetAsync("gitflow.prefix.versiontag", version, true).ConfigureAwait(false);

            var init = new Command();
            init.WorkingDirectory = repo;
            init.Context = repo;
            init.Args = "flow init -d";
            return await init.Use(log).ExecAsync().ConfigureAwait(false);
        }

        public static async Task<bool> StartAsync(string repo, Models.GitFlowBranchType type, string name, Models.ICommandLog log)
        {
            var start = new Command();
            start.WorkingDirectory = repo;
            start.Context = repo;

            var typeStr = BranchTypeToString(type);
            if (typeStr == null)
            {
                App.RaiseException(repo, "Bad git-flow branch type!!!");
                return false;
            }
            start.Args = $"flow {typeStr} start {name}";

            return await start.Use(log).ExecAsync().ConfigureAwait(false);
        }

        public static async Task<bool> FinishAsync(string repo, Models.GitFlowBranchType type, string name, bool squash, bool push, bool keepBranch, Models.ICommandLog log)
        {
            var builder = new StringBuilder();
            builder.Append("flow ");

            var typeStr = BranchTypeToString(type);
            if (typeStr == null)
            {
                App.RaiseException(repo, "Bad git-flow branch type!!!");
                return false;
            }
            builder.Append(typeStr);

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
            return await finish.Use(log).ExecAsync().ConfigureAwait(false);
        }

        private static string BranchTypeToString(Models.GitFlowBranchType type) =>
            type switch
            {
                Models.GitFlowBranchType.Feature => "feature",
                Models.GitFlowBranchType.Release => "release",
                Models.GitFlowBranchType.Hotfix => "hotfix",
                _ => null
            };
    }
}
