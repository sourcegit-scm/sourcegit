using System.IO;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public abstract class InProgressContext
    {
        public async Task ContinueAsync()
        {
            if (_continueCmd != null)
                await _continueCmd.ExecAsync();
        }

        public async Task SkipAsync()
        {
            if (_skipCmd != null)
                await _skipCmd.ExecAsync();
        }

        public async Task AbortAsync()
        {
            if (_abortCmd != null)
                await _abortCmd.ExecAsync();
        }

        protected Commands.Command _continueCmd = null;
        protected Commands.Command _skipCmd = null;
        protected Commands.Command _abortCmd = null;
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

        public CherryPickInProgress(Repository repo)
        {
            _continueCmd = new Commands.Command
            {
                WorkingDirectory = repo.FullPath,
                Context = repo.FullPath,
                Args = "cherry-pick --continue",
            };

            _skipCmd = new Commands.Command
            {
                WorkingDirectory = repo.FullPath,
                Context = repo.FullPath,
                Args = "cherry-pick --skip",
            };

            _abortCmd = new Commands.Command
            {
                WorkingDirectory = repo.FullPath,
                Context = repo.FullPath,
                Args = "cherry-pick --abort",
            };

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

        public RebaseInProgress(Repository repo)
        {
            _continueCmd = new Commands.Command
            {
                WorkingDirectory = repo.FullPath,
                Context = repo.FullPath,
                Editor = Commands.Command.EditorType.RebaseEditor,
                Args = "rebase --continue",
            };

            _skipCmd = new Commands.Command
            {
                WorkingDirectory = repo.FullPath,
                Context = repo.FullPath,
                Args = "rebase --skip",
            };

            _abortCmd = new Commands.Command
            {
                WorkingDirectory = repo.FullPath,
                Context = repo.FullPath,
                Args = "rebase --abort",
            };

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
    }

    public class RevertInProgress : InProgressContext
    {
        public Models.Commit Head
        {
            get;
        }

        public RevertInProgress(Repository repo)
        {
            _continueCmd = new Commands.Command
            {
                WorkingDirectory = repo.FullPath,
                Context = repo.FullPath,
                Args = "revert --continue",
            };

            _skipCmd = new Commands.Command
            {
                WorkingDirectory = repo.FullPath,
                Context = repo.FullPath,
                Args = "revert --skip",
            };

            _abortCmd = new Commands.Command
            {
                WorkingDirectory = repo.FullPath,
                Context = repo.FullPath,
                Args = "revert --abort",
            };

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

        public MergeInProgress(Repository repo)
        {
            _continueCmd = new Commands.Command
            {
                WorkingDirectory = repo.FullPath,
                Context = repo.FullPath,
                Args = "merge --continue",
            };

            _abortCmd = new Commands.Command
            {
                WorkingDirectory = repo.FullPath,
                Context = repo.FullPath,
                Args = "merge --abort",
            };

            Current = new Commands.QueryCurrentBranch(repo.FullPath).GetResult();

            var sourceSHA = File.ReadAllText(Path.Combine(repo.GitDir, "MERGE_HEAD")).Trim();
            Source = new Commands.QuerySingleCommit(repo.FullPath, sourceSHA).GetResult() ?? new Models.Commit() { SHA = sourceSHA };
            SourceName = Source.GetFriendlyName();
        }
    }
}
