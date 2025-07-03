using System.IO;

namespace SourceGit.ViewModels
{
    public abstract class InProgressContext
    {
        protected InProgressContext(string repo, string cmd)
        {
            _repo = repo;
            _cmd = cmd;
        }

        public bool Abort()
        {
            return new Commands.Command()
            {
                WorkingDirectory = _repo,
                Context = _repo,
                Args = $"{_cmd} --abort",
            }.Exec();
        }

        public virtual bool Skip()
        {
            return new Commands.Command()
            {
                WorkingDirectory = _repo,
                Context = _repo,
                Args = $"{_cmd} --skip",
            }.Exec();
        }

        public virtual bool Continue()
        {
            return new Commands.Command()
            {
                WorkingDirectory = _repo,
                Context = _repo,
                Editor = Commands.Command.EditorType.None,
                Args = $"{_cmd} --continue",
            }.Exec();
        }

        protected string GetFriendlyNameOfCommit(Models.Commit commit)
        {
            var branchDecorator = commit.Decorators.Find(x => x.Type == Models.DecoratorType.LocalBranchHead || x.Type == Models.DecoratorType.RemoteBranchHead);
            if (branchDecorator != null)
                return branchDecorator.Name;

            var tagDecorator = commit.Decorators.Find(x => x.Type == Models.DecoratorType.Tag);
            if (tagDecorator != null)
                return tagDecorator.Name;

            return commit.SHA.Substring(0, 10);
        }

        protected string _repo = string.Empty;
        protected string _cmd = string.Empty;
    }

    public class CherryPickInProgress : InProgressContext
    {
        public Models.Commit Head
        {
            get;
            private set;
        }

        public string HeadName
        {
            get => GetFriendlyNameOfCommit(Head);
        }

        public static CherryPickInProgress Create(Repository repo)
        {
            var ipc = new CherryPickInProgress(repo);
            var headSHA = File.ReadAllText(Path.Combine(repo.GitDir, "CHERRY_PICK_HEAD")).Trim();
            ipc.Head = new Commands.QuerySingleCommit(repo.FullPath, headSHA).Result() ?? new Models.Commit() { SHA = headSHA };
            return ipc;
        }

        private CherryPickInProgress(Repository repo) : base(repo.FullPath, "cherry-pick")
        {
        }
    }

    public class RebaseInProgress : InProgressContext
    {
        public string HeadName
        {
            get;
            private set;
        }

        public string BaseName
        {
            get => GetFriendlyNameOfCommit(Onto);
        }

        public Models.Commit StoppedAt
        {
            get;
            private set;
        }

        public Models.Commit Onto
        {
            get;
            private set;
        }

        public static RebaseInProgress Create(Repository repo)
        {
            var ipc = new RebaseInProgress(repo);
            ipc.HeadName = File.ReadAllText(Path.Combine(repo.GitDir, "rebase-merge", "head-name")).Trim();
            if (ipc.HeadName.StartsWith("refs/heads/"))
                ipc.HeadName = ipc.HeadName.Substring(11);
            else if (ipc.HeadName.StartsWith("refs/tags/"))
                ipc.HeadName = ipc.HeadName.Substring(10);

            var stoppedSHAPath = Path.Combine(repo.GitDir, "rebase-merge", "stopped-sha");
            var stoppedSHA = File.Exists(stoppedSHAPath)
                ? File.ReadAllText(stoppedSHAPath).Trim()
                : new Commands.QueryRevisionByRefName(repo.FullPath, ipc.HeadName).Result();

            if (!string.IsNullOrEmpty(stoppedSHA))
                ipc.StoppedAt = new Commands.QuerySingleCommit(repo.FullPath, stoppedSHA).Result() ?? new Models.Commit() { SHA = stoppedSHA };

            var ontoSHA = File.ReadAllText(Path.Combine(repo.GitDir, "rebase-merge", "onto")).Trim();
            ipc.Onto = new Commands.QuerySingleCommit(repo.FullPath, ontoSHA).Result() ?? new Models.Commit() { SHA = ontoSHA };
            return ipc;
        }

        private RebaseInProgress(Repository repo) : base(repo.FullPath, "rebase")
        {
        }

        public override bool Continue()
        {
            return new Commands.Command()
            {
                WorkingDirectory = _repo,
                Context = _repo,
                Editor = Commands.Command.EditorType.RebaseEditor,
                Args = "rebase --continue",
            }.Exec();
        }
    }

    public class RevertInProgress : InProgressContext
    {
        public Models.Commit Head
        {
            get;
            private set;
        }

        public static RevertInProgress Create(Repository repo)
        {
            var ipc = new RevertInProgress(repo);
            var headSHA = File.ReadAllText(Path.Combine(repo.GitDir, "REVERT_HEAD")).Trim();
            ipc.Head = new Commands.QuerySingleCommit(repo.FullPath, headSHA).Result() ?? new Models.Commit() { SHA = headSHA };
            return ipc;
        }

        private RevertInProgress(Repository repo) : base(repo.FullPath, "revert")
        {
        }
    }

    public class MergeInProgress : InProgressContext
    {
        public string Current
        {
            get;
            private set;
        }

        public Models.Commit Source
        {
            get;
            private set;
        }

        public string SourceName
        {
            get => GetFriendlyNameOfCommit(Source);
        }

        public static MergeInProgress Create(Repository repo)
        {
            var ipc = new MergeInProgress(repo);
            ipc.Current = Commands.Branch.ShowCurrent(repo.FullPath);

            var sourceSHA = File.ReadAllText(Path.Combine(repo.GitDir, "MERGE_HEAD")).Trim();
            ipc.Source = new Commands.QuerySingleCommit(repo.FullPath, sourceSHA).Result() ?? new Models.Commit() { SHA = sourceSHA };
            return ipc;
        }

        private MergeInProgress(Repository repo) : base(repo.FullPath, "merge")
        {
        }

        public override bool Skip()
        {
            return true;
        }
    }
}
