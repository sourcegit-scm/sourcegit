using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class ForceSquashAcrossMerges : Popup
    {
        public Models.Commit Target { get; }
        public bool CreateBackup { get; set; } = true;
        public bool AutoStash { get; set; } = true;
        public bool KeepAuthorDate { get; set; }
        public bool AppendMessages { get; set; }
        public string Message { get => _message; set => SetProperty(ref _message, value, true); }

        public ForceSquashAcrossMerges(Repository repo, Models.Commit target)
        {
            _repo = repo;
            Target = target;
            _message = target.Subject;
        }

        public override async Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Squashing ...";
            var log = _repo.CreateLog("ForceSquash");
            Use(log);

            var baseSHA = Target.Parents[0];
            var signOff = _repo.Settings.EnableSignOffForCommit;
            var stashName = string.Empty;
            var succ = true;
            var head = await new Commands.QueryRevisionByRefName(_repo.FullPath, "HEAD").GetResultAsync();
            var headShort = head[..7];

            if (AutoStash)
            {
                var changes = await new Commands.QueryLocalChanges(_repo.FullPath, false).GetResultAsync();
                foreach (var c in changes)
                {
                    if (c.Index != Models.ChangeState.None || c.WorkTree != Models.ChangeState.None)
                    {
                        stashName = $"sourcegit/force-squash/{headShort}-{Guid.NewGuid().ToString("N")[..6]}";
                        succ = await new Commands.Stash(_repo.FullPath).Use(log).PushAsync(stashName);
                        break;
                    }
                }
                if (!succ)
                {
                    log.Complete();
                    _repo.SetWatcherEnabled(true);
                    return false;
                }
            }

            var backupName = string.Empty;
            if (CreateBackup && _repo.CurrentBranch != null)
            {
                backupName = $"sourcegit/backup/flatten-{Models.Branch.FixName(_repo.CurrentBranch.Name)}-{_repo.CurrentBranch.Head[..7]}";
                succ = await new Commands.Branch(_repo.FullPath, backupName).Use(log).CreateAsync("HEAD", false);
                if (!succ)
                {
                    log.Complete();
                    _repo.SetWatcherEnabled(true);
                    return false;
                }
            }

            List<Models.Commit> append = null;
            if (AppendMessages)
            {
                append = await new Commands.QueryCommits(_repo.FullPath, $"{Target.SHA}..{head}", false).GetResultAsync();
                append.Sort((l, r) => l.CommitterTime.CompareTo(r.CommitterTime));
            }

            succ = await new Commands.Reset(_repo.FullPath, baseSHA, "--soft").Use(log).ExecAsync();
            if (!succ)
            {
                log.Complete();
                _repo.SetWatcherEnabled(true);
                return false;
            }

            var commitMsg = Message;
            if (AppendMessages && append.Count > 0)
            {
                var lines = new List<string>();
                foreach (var c in append)
                {
                    var msg = c.Subject.Trim();
                    if (msg.Length == 0)
                        continue;
                    if (!lines.Contains(msg))
                        lines.Add(msg);
                }
                if (lines.Count > 0)
                    commitMsg += "\n\n" + string.Join("\n", lines);
            }

            var commit = new Commands.Commit(_repo.FullPath, commitMsg, signOff, false, false);
            if (KeepAuthorDate)
            {
                var author = Target.Author;
                var date = DateTimeOffset.FromUnixTimeSeconds((long)Target.AuthorTime).ToString("o");
                commit.Args += $" --author={("" + author.Name + " <" + author.Email + ">").Quoted()} --date={date.Quoted()}";
                commit.Envs["GIT_COMMITTER_DATE"] = date;
            }
            succ = await commit.Use(log).RunAsync();
            if (!succ)
            {
                log.Complete();
                _repo.SetWatcherEnabled(true);
                return false;
            }

            if (!string.IsNullOrEmpty(stashName))
            {
                succ = await new Commands.Stash(_repo.FullPath).Use(log).PopAsync(stashName);
                if (!succ)
                {
                    App.SendNotification(_repo.FullPath, App.Text("ForceSquash.StashPopFailed"));
                    log.Complete();
                    _repo.SetWatcherEnabled(true);
                    return false;
                }
            }

            log.Complete();
            _repo.SetWatcherEnabled(true);
            _repo.RefreshCommits();
            if (!string.IsNullOrEmpty(backupName))
                App.SendNotification(_repo.FullPath, App.Text("ForceSquash.Success", backupName));
            else
                App.SendNotification(_repo.FullPath, App.Text("ForceSquash.SuccessNoBackup"));
            _repo.ShowPopup(new Push(_repo, _repo.CurrentBranch) { ForcePush = true });
            return true;
        }

        private readonly Repository _repo;
        private string _message;

    }
}
