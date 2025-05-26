using System.Text;
using Avalonia.Threading;

namespace SourceGit.Commands
{
    public static class GitFlow
    {
        public static bool Init(string repo, string master, string develop, string feature, string release, string hotfix, string version, Models.ICommandLog log)
        {
            var config = new Config(repo);
            config.Set("gitflow.branch.master", master);
            config.Set("gitflow.branch.develop", develop);
            config.Set("gitflow.prefix.feature", feature);
            config.Set("gitflow.prefix.bugfix", "bugfix/");
            config.Set("gitflow.prefix.release", release);
            config.Set("gitflow.prefix.hotfix", hotfix);
            config.Set("gitflow.prefix.support", "support/");
            config.Set("gitflow.prefix.versiontag", version, true);

            var init = new Command();
            init.WorkingDirectory = repo;
            init.Context = repo;
            init.Args = "flow init -d";
            init.Log = log;
            return init.Exec();
        }

        public static bool Start(string repo, Models.GitFlowBranchType type, string name, Models.ICommandLog log)
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
                    Dispatcher.UIThread.Invoke(() => App.RaiseException(repo, "Bad git-flow branch type!!!"));
                    return false;
            }

            start.Log = log;
            return start.Exec();
        }

        public static bool Finish(string repo, Models.GitFlowBranchType type, string name, bool squash, bool push, bool keepBranch, Models.ICommandLog log)
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
                    Dispatcher.UIThread.Invoke(() => App.RaiseException(repo, "Bad git-flow branch type!!!"));
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
            return finish.Exec();
        }
    }
}
