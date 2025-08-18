using System.IO;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public abstract class InProgressContext
    {
        protected InProgressContext(string repo, string cmd)
        {
            _repo = repo;
            _cmd = cmd;
        }

        public async Task<bool> AbortAsync()
        {
            return await new Commands.Command()
            {
                WorkingDirectory = _repo,
                Context = _repo,
                Args = $"{_cmd} --abort",
            }.ExecAsync();
        }

        public virtual async Task<bool> SkipAsync()
        {
            return await new Commands.Command()
            {
                WorkingDirectory = _repo,
                Context = _repo,
                Args = $"{_cmd} --skip",
            }.ExecAsync();
        }

        public virtual async Task<bool> ContinueAsync()
        {
            return await new Commands.Command()
            {
                WorkingDirectory = _repo,
                Context = _repo,
                Editor = Commands.Command.EditorType.None,
                Args = $"{_cmd} --continue",
            }.ExecAsync();
        }

        protected string _repo = string.Empty;
        protected string _cmd = string.Empty;
    }

    public class CherryPickInProgress : InProgressContext
    {
        public Models.Commit Head
        {
            get;
        }

        public string HeadName
        {
            get;
        }

        public CherryPickInProgress(Repository repo) : base(repo.FullPath, "cherry-pick")
        {
            var headSHA = File.ReadAllText(Path.Combine(repo.GitDir, "CHERRY_PICK_HEAD")).Trim();
            Head = new Commands.QuerySingleCommit(repo.FullPath, headSHA).GetResult() ?? new Models.Commit() { SHA = headSHA };
            HeadName = Head.GetFriendlyName();
        }
    }

    public class RebaseInProgress : InProgressContext
    {
        public string HeadName
        {
            get;
        }

        public string BaseName
        {
            get;
        }

        public Models.Commit StoppedAt
        {
            get;
        }

        public Models.Commit Onto
        {
            get;
        }

        public RebaseInProgress(Repository repo) : base(repo.FullPath, "rebase")
        {
            HeadName = File.ReadAllText(Path.Combine(repo.GitDir, "rebase-merge", "head-name")).Trim();
            if (HeadName.StartsWith("refs/heads/"))
                HeadName = HeadName.Substring(11);
            else if (HeadName.StartsWith("refs/tags/"))
                HeadName = HeadName.Substring(10);

            var stoppedSHAPath = Path.Combine(repo.GitDir, "rebase-merge", "stopped-sha");
            var stoppedSHA = File.Exists(stoppedSHAPath)
                ? File.ReadAllText(stoppedSHAPath).Trim()
                : new Commands.QueryRevisionByRefName(repo.FullPath, HeadName).GetResult();

            if (!string.IsNullOrEmpty(stoppedSHA))
                StoppedAt = new Commands.QuerySingleCommit(repo.FullPath, stoppedSHA).GetResult() ?? new Models.Commit() { SHA = stoppedSHA };

            var ontoSHA = File.ReadAllText(Path.Combine(repo.GitDir, "rebase-merge", "onto")).Trim();
            Onto = new Commands.QuerySingleCommit(repo.FullPath, ontoSHA).GetResult() ?? new Models.Commit() { SHA = ontoSHA };
            BaseName = Onto.GetFriendlyName();
        }

        public override async Task<bool> ContinueAsync()
        {
            return await new Commands.Command()
            {
                WorkingDirectory = _repo,
                Context = _repo,
                Editor = Commands.Command.EditorType.RebaseEditor,
                Args = "rebase --continue",
            }.ExecAsync();
        }
    }

    public class RevertInProgress : InProgressContext
    {
        public Models.Commit Head
        {
            get;
        }

        public RevertInProgress(Repository repo) : base(repo.FullPath, "revert")
        {
            var headSHA = File.ReadAllText(Path.Combine(repo.GitDir, "REVERT_HEAD")).Trim();
            Head = new Commands.QuerySingleCommit(repo.FullPath, headSHA).GetResult() ?? new Models.Commit() { SHA = headSHA };
        }
    }

    public class MergeInProgress : InProgressContext
    {
        public string Current
        {
            get;
        }

        public Models.Commit Source
        {
            get;
        }

        public string SourceName
        {
            get;
        }

        public MergeInProgress(Repository repo) : base(repo.FullPath, "merge")
        {
            Current = new Commands.QueryCurrentBranch(repo.FullPath).GetResult();

            var sourceSHA = File.ReadAllText(Path.Combine(repo.GitDir, "MERGE_HEAD")).Trim();
            Source = new Commands.QuerySingleCommit(repo.FullPath, sourceSHA).GetResult() ?? new Models.Commit() { SHA = sourceSHA };
            SourceName = Source.GetFriendlyName();
        }

        public override async Task<bool> SkipAsync()
        {
            return await Task.FromResult(true);
        }
    }
}
