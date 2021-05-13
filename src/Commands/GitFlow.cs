namespace SourceGit.Commands {
    /// <summary>
    ///     Git-Flow命令
    /// </summary>
    public class GitFlow : Command {

        public GitFlow(string repo) {
            Cwd = repo;
        }

        public bool Init(string master, string develop, string feature, string release, string hotfix, string version) {
            var branches = new Branches(Cwd).Result();
            var current = branches.Find(x => x.IsCurrent);

            var masterBranch = branches.Find(x => x.Name == master);
            if (masterBranch == null) new Branch(Cwd, master).Create(current.Head);

            var devBranch = branches.Find(x => x.Name == develop);
            if (devBranch == null) new Branch(Cwd, develop).Create(current.Head);

            var cmd = new Config(Cwd);
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

        public void Start(Models.GitFlowBranchType type, string name) {
            switch (type) {
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
                return;
            }

            Exec();
        }

        public void Finish(Models.GitFlowBranchType type, string name, bool keepBranch) {
            var option = keepBranch ? "-k" : string.Empty;
            switch (type) {
            case Models.GitFlowBranchType.Feature:
                Args = $"flow feature finish {option} {name}";
                break;
            case Models.GitFlowBranchType.Release:
                Args = $"flow release finish {option} {name}";
                break;
            case Models.GitFlowBranchType.Hotfix:
                Args = $"flow hotfix finish {option} {name}";
                break;
            default:
                return;
            }

            Exec();
        }
    }
}
