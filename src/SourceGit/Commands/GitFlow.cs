using System.Collections.Generic;

using Avalonia.Threading;

namespace SourceGit.Commands
{
    public class GitFlow : Command
    {
        public GitFlow(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
        }

        public bool Init(List<Models.Branch> branches, string master, string develop, string feature, string release, string hotfix, string version)
        {
            var current = branches.Find(x => x.IsCurrent);

            var masterBranch = branches.Find(x => x.Name == master);
            if (masterBranch == null && current != null)
                Branch.Create(WorkingDirectory, master, current.Head);

            var devBranch = branches.Find(x => x.Name == develop);
            if (devBranch == null && current != null)
                Branch.Create(WorkingDirectory, develop, current.Head);

            var cmd = new Config(WorkingDirectory);
            cmd.Set("gitflow.branch.master", master);
            cmd.Set("gitflow.branch.develop", develop);
            cmd.Set("gitflow.prefix.feature", feature);
            cmd.Set("gitflow.prefix.bugfix", "bugfix/");
            cmd.Set("gitflow.prefix.release", release);
            cmd.Set("gitflow.prefix.hotfix", hotfix);
            cmd.Set("gitflow.prefix.support", "support/");
            cmd.Set("gitflow.prefix.versiontag", version, true);

            Args = "flow init -d";
            return Exec();
        }

        public bool Start(Models.GitFlowBranchType type, string name)
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
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        App.RaiseException(Context, "Bad branch type!!!");
                    });
                    return false;
            }

            return Exec();
        }

        public bool Finish(Models.GitFlowBranchType type, string name, bool keepBranch)
        {
            var option = keepBranch ? "-k" : string.Empty;
            switch (type)
            {
                case Models.GitFlowBranchType.Feature:
                    Args = $"flow feature finish {option} {name}";
                    break;
                case Models.GitFlowBranchType.Release:
                    Args = $"flow release finish {option} {name} -m \"RELEASE_DONE\"";
                    break;
                case Models.GitFlowBranchType.Hotfix:
                    Args = $"flow hotfix finish {option} {name} -m \"HOTFIX_DONE\"";
                    break;
                default:
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        App.RaiseException(Context, "Bad branch type!!!");
                    });
                    return false;
            }

            return Exec();
        }
    }
}
