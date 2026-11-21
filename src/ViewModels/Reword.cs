using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class Reword : Popup
    {
        public Models.Commit Target
        {
            get;
        }

        public string Message
        {
            get;
        }

        public Reword(Repository repo, Models.Commit target, string message)
        {
            _repo = repo;
            Target = target;
            Message = message;
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = $"Reword commit '{Target.SHA}' ...";

            var log = _repo.CreateLog($"Reword '{Target.SHA}'");
            Use(log);

            var succ = Target.IsCurrentHead
                ? await RewordHeadAsync(log)
                : await RewordWithRebaseAsync(log);

            log.Complete();

            if (succ)
            {
                // The rewritten commit sits at the same distance from HEAD as the original did,
                // so re-select it once the history is reloaded instead of jumping back to the top.
                var sha = await new Commands.QueryRevisionByRefName(_repo.FullPath, $"HEAD~{_distance}").GetResultAsync();
                if (!string.IsNullOrEmpty(sha))
                    _repo.NavigateToCommit(sha, true);
            }

            _repo.MarkBranchesDirtyManually();
            return true;
        }

        private async Task<bool> RewordHeadAsync(CommandLog log)
        {
            _distance = 0;
            return await new Commands.Reword(_repo.FullPath, Message)
                .Use(log)
                .RunAsync();
        }

        private async Task<bool> RewordWithRebaseAsync(CommandLog log)
        {
            var onto = Target.Parents[0];
            var commits = await new Commands.QueryCommitsForInteractiveRebase(_repo.FullPath, onto).GetResultAsync();

            var collection = new Models.InteractiveRebaseJobCollection();
            collection.OrigHead = _repo.CurrentBranch.Head;
            collection.Onto = onto;

            _distance = -1;
            for (var i = commits.Count - 1; i >= 0; i--)
            {
                var c = commits[i];
                var isTarget = c.Commit.SHA.Equals(Target.SHA, StringComparison.Ordinal);
                if (isTarget)
                    _distance = i;

                collection.Jobs.Add(new Models.InteractiveRebaseJob()
                {
                    SHA = c.Commit.SHA,
                    Action = isTarget ? Models.InteractiveRebaseAction.Reword : Models.InteractiveRebaseAction.Pick,
                    Message = isTarget ? Message : c.Message,
                });
            }

            if (_distance < 0)
            {
                _repo.SendNotification($"Commit '{Target.SHA}' is not reachable from HEAD!", true);
                return false;
            }

            var jobsFile = Path.Combine(_repo.GitDir, "sourcegit.interactive_rebase");
            await using (var stream = File.Create(jobsFile))
            {
                await JsonSerializer.SerializeAsync(stream, collection, JsonCodeGen.Default.InteractiveRebaseJobCollection);
            }

            return await new Commands.InteractiveRebase(_repo.FullPath, onto, true, false)
                .Use(log)
                .ExecAsync();
        }

        private readonly Repository _repo = null;
        private int _distance = 0;
    }
}
