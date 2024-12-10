using System.IO;

namespace SourceGit.ViewModels
{
    public abstract class InProgressContext
    {
        public string Repository
        {
            get;
            set;
        }

        public string Cmd
        {
            get;
            set;
        }

        public bool CanSkip
        {
            get;
            protected set;
        }

        public InProgressContext(string repo, string cmd, bool canSkip)
        {
            Repository = repo;
            Cmd = cmd;
            CanSkip = canSkip;
        }

        public bool Abort()
        {
            return new Commands.Command()
            {
                WorkingDirectory = Repository,
                Context = Repository,
                Args = $"{Cmd} --abort",
            }.Exec();
        }

        public bool Skip()
        {
            if (!CanSkip)
                return true;

            return new Commands.Command()
            {
                WorkingDirectory = Repository,
                Context = Repository,
                Args = $"{Cmd} --skip",
            }.Exec();
        }

        public virtual bool Continue()
        {
            return new Commands.Command()
            {
                WorkingDirectory = Repository,
                Context = Repository,
                Editor = Commands.Command.EditorType.None,
                Args = $"{Cmd} --continue",
            }.Exec();
        }
    }

    public class CherryPickInProgress : InProgressContext
    {
        public Models.Commit Head
        {
            get;
            private set;
        }

        public CherryPickInProgress(Repository repo) : base(repo.FullPath, "cherry-pick", true) 
        {
            var headSHA = File.ReadAllText(Path.Combine(repo.GitDir, "CHERRY_PICK_HEAD")).Trim();
            Head = new Commands.QuerySingleCommit(repo.FullPath, headSHA).Result();
        }
    }

    public class RebaseInProgress : InProgressContext
    {
        public Models.Commit StoppedAt
        {
            get;
            private set;
        }

        public RebaseInProgress(Repository repo) : base(repo.FullPath, "rebase", true)
        {
            _gitDir = repo.GitDir;

            var stoppedSHA = File.ReadAllText(Path.Combine(repo.GitDir, "rebase-merge", "stopped-sha")).Trim();
            StoppedAt = new Commands.QuerySingleCommit(repo.FullPath, stoppedSHA).Result();
        }

        public override bool Continue()
        {
            var succ = new Commands.Command()
            {
                WorkingDirectory = Repository,
                Context = Repository,
                Editor = Commands.Command.EditorType.RebaseEditor,
                Args = $"rebase --continue",
            }.Exec();

            if (succ)
            {
                var jobsFile = Path.Combine(_gitDir, "sourcegit_rebase_jobs.json");
                var rebaseMergeHead = Path.Combine(_gitDir, "REBASE_HEAD");
                var rebaseMergeFolder = Path.Combine(_gitDir, "rebase-merge");
                var rebaseApplyFolder = Path.Combine(_gitDir, "rebase-apply");
                if (File.Exists(jobsFile))
                    File.Delete(jobsFile);
                if (File.Exists(rebaseMergeHead))
                    File.Delete(rebaseMergeHead);
                if (Directory.Exists(rebaseMergeFolder))
                    Directory.Delete(rebaseMergeFolder);
                if (Directory.Exists(rebaseApplyFolder))
                    Directory.Delete(rebaseApplyFolder);
            }

            return succ;
        }

        private string _gitDir;
    }

    public class RevertInProgress : InProgressContext
    {
        public RevertInProgress(string repo) : base(repo, "revert", false) { }
    }

    public class MergeInProgress : InProgressContext
    {
        public MergeInProgress(string repo) : base(repo, "merge", false) { }
    }
}
