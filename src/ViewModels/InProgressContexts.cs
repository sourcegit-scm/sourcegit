﻿using System.IO;

namespace SourceGit.ViewModels
{
    public abstract class InProgressContext
    {
        public InProgressContext(string repo, string cmd)
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

        public CherryPickInProgress(Repository repo) : base(repo.FullPath, "cherry-pick")
        {
            var headSHA = File.ReadAllText(Path.Combine(repo.GitDir, "CHERRY_PICK_HEAD")).Trim();
            Head = new Commands.QuerySingleCommit(repo.FullPath, headSHA).Result() ?? new Models.Commit() { SHA = headSHA };
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

        public RebaseInProgress(Repository repo) : base(repo.FullPath, "rebase")
        {
            HeadName = File.ReadAllText(Path.Combine(repo.GitDir, "rebase-merge", "head-name")).Trim();
            if (HeadName.StartsWith("refs/heads/"))
                HeadName = HeadName.Substring(11);
            else if (HeadName.StartsWith("refs/tags/"))
                HeadName = HeadName.Substring(10);

            var stoppedSHAPath = Path.Combine(repo.GitDir, "rebase-merge", "stopped-sha");
            var stoppedSHA = string.Empty;
            if (File.Exists(stoppedSHAPath))
                stoppedSHA = File.ReadAllText(stoppedSHAPath).Trim();
            else
                stoppedSHA = new Commands.QueryRevisionByRefName(repo.FullPath, HeadName).Result();

            if (!string.IsNullOrEmpty(stoppedSHA))
                StoppedAt = new Commands.QuerySingleCommit(repo.FullPath, stoppedSHA).Result() ?? new Models.Commit() { SHA = stoppedSHA };

            var ontoSHA = File.ReadAllText(Path.Combine(repo.GitDir, "rebase-merge", "onto")).Trim();
            Onto = new Commands.QuerySingleCommit(repo.FullPath, ontoSHA).Result() ?? new Models.Commit() { SHA = ontoSHA };
        }

        public override bool Continue()
        {
            return new Commands.Command()
            {
                WorkingDirectory = _repo,
                Context = _repo,
                Editor = Commands.Command.EditorType.RebaseEditor,
                Args = $"rebase --continue",
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

        public RevertInProgress(Repository repo) : base(repo.FullPath, "revert")
        {
            var headSHA = File.ReadAllText(Path.Combine(repo.GitDir, "REVERT_HEAD")).Trim();
            Head = new Commands.QuerySingleCommit(repo.FullPath, headSHA).Result() ?? new Models.Commit() { SHA = headSHA };
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

        public MergeInProgress(Repository repo) : base(repo.FullPath, "merge")
        {
            Current = Commands.Branch.ShowCurrent(repo.FullPath);

            var sourceSHA = File.ReadAllText(Path.Combine(repo.GitDir, "MERGE_HEAD")).Trim();
            Source = new Commands.QuerySingleCommit(repo.FullPath, sourceSHA).Result() ?? new Models.Commit() { SHA = sourceSHA };
        }

        public override bool Skip()
        {
            return true;
        }
    }
}
