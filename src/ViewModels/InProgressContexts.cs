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

        public InProgressContext(string repo, string cmd)
        {
            Repository = repo;
            Cmd = cmd;
        }

        public bool Abort()
        {
            return new Commands.Command()
            {
                WorkingDirectory = Repository,
                Context = Repository,
                Args = [Cmd, "--abort"],
            }.Exec();
        }

        public virtual bool Continue()
        {
            return new Commands.Command()
            {
                WorkingDirectory = Repository,
                Context = Repository,
                Editor = Commands.Command.EditorType.None,
                Args = [Cmd, "--continue"],
            }.Exec();
        }
    }

    public class CherryPickInProgress : InProgressContext
    {
        public CherryPickInProgress(string repo) : base(repo, "cherry-pick") { }
    }

    public class RebaseInProgress : InProgressContext
    {
        public RebaseInProgress(Repository repo) : base(repo.FullPath, "rebase")
        {
            _gitDir = repo.GitDir;
        }

        public override bool Continue()
        {
            var succ = new Commands.Command()
            {
                WorkingDirectory = Repository,
                Context = Repository,
                Editor = Commands.Command.EditorType.RebaseEditor,
                Args = ["rebase", "--continue"],
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
        public RevertInProgress(string repo) : base(repo, "revert") { }
    }

    public class MergeInProgress : InProgressContext
    {
        public MergeInProgress(string repo) : base(repo, "merge") { }
    }
}
