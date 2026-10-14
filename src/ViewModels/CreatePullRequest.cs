using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

using Avalonia.Threading;

namespace SourceGit.ViewModels
{
    public class CreatePullRequest : Popup
    {
        public Models.Branch Source
        {
            get;
        }

        public bool NeedsPush
        {
            get;
        }

        public bool PushFirst
        {
            get;
            set;
        }

        public bool IsPushRemoteSelectable
        {
            get => _upstream == null;
        }

        public List<Models.Remote> PushRemotes
        {
            get;
        }

        public Models.Remote SelectedPushRemote
        {
            get => _selectedPushRemote;
            set
            {
                if (SetProperty(ref _selectedPushRemote, value))
                    RefreshTargetBranches();
            }
        }

        public List<Models.Branch> TargetBranches
        {
            get => _targetBranches;
            private set => SetProperty(ref _targetBranches, value);
        }

        [Required(ErrorMessage = "Target branch is required!!!")]
        [CustomValidation(typeof(CreatePullRequest), nameof(ValidateTarget))]
        public Models.Branch SelectedTarget
        {
            get => _selectedTarget;
            set
            {
                if (SetProperty(ref _selectedTarget, value, true))
                    PostTargetChanged();
            }
        }

        public List<Models.PullRequestPlatform> Platforms
        {
            get => Models.PullRequestPlatform.Supported;
        }

        public bool IsPlatformSelectable
        {
            get => _isPlatformSelectable;
            private set => SetProperty(ref _isPlatformSelectable, value);
        }

        [Required(ErrorMessage = "Platform is required!!!")]
        public Models.PullRequestPlatform SelectedPlatform
        {
            get => _selectedPlatform;
            set
            {
                if (SetProperty(ref _selectedPlatform, value, true))
                {
                    OnPropertyChanged(nameof(IsClipboardHintVisible));
                    ValidateProperty(_selectedTarget, nameof(SelectedTarget));
                }
            }
        }

        public bool IsClipboardHintVisible
        {
            get => _selectedPlatform != null && (!_selectedPlatform.SupportsTitle || !_selectedPlatform.SupportsBody);
        }

        public List<Models.PullRequestTemplate> Templates
        {
            get => _templates;
            private set => SetProperty(ref _templates, value);
        }

        public Models.PullRequestTemplate SelectedTemplate
        {
            get => _selectedTemplate;
            set
            {
                if (SetProperty(ref _selectedTemplate, value))
                    UpdateDefaultContent();
            }
        }

        [Required(ErrorMessage = "Title is required!!!")]
        public string Title
        {
            get => _title;
            set
            {
                if (SetProperty(ref _title, value, true))
                    _isTitleEdited = true;
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                // NOTE: TextBox may normalize line endings of generated content, which should not be treated as an edit.
                if (SetProperty(ref _description, value) && !_generatedDescription.Equals(value?.ReplaceLineEndings("\n"), StringComparison.Ordinal))
                    _isDescriptionEdited = true;
            }
        }

        public CreatePullRequest(Repository repo, Models.Branch source, Models.Branch target)
        {
            _repo = repo;
            Source = source;

            if (!string.IsNullOrEmpty(source.Upstream) && !source.IsUpstreamGone)
                _upstream = repo.Branches.Find(x => x.FullName.Equals(source.Upstream, StringComparison.Ordinal));

            NeedsPush = _upstream == null || source.Ahead.Count > 0;
            PushFirst = NeedsPush;

            PushRemotes = repo.Remotes;
            _selectedPushRemote = _upstream != null ? repo.Remotes.Find(x => x.Name.Equals(_upstream.Remote, StringComparison.Ordinal)) : GetDefaultPushRemote(target);

            RefreshTargetBranches();

            UpdateDefaultContent();

            if (target != null)
                SelectedTarget = target;
            else
                SelectDefaultTarget();
        }

        public static ValidationResult ValidateTarget(Models.Branch target, ValidationContext ctx)
        {
            if (ctx.ObjectInstance is CreatePullRequest { SelectedPlatform: { SupportsCrossRepo: false } } creator &&
                target != null &&
                !target.Remote.Equals(creator._selectedPushRemote?.Name, StringComparison.Ordinal))
            {
                return new ValidationResult($"{creator.SelectedPlatform.Name} does not support pull requests between different repositories here!");
            }

            return ValidationResult.Success;
        }

        public override async Task<bool> Sure()
        {
            var sourceRemote = _selectedPushRemote;
            var targetRemote = _repo.Remotes.Find(x => x.Name.Equals(_selectedTarget.Remote, StringComparison.Ordinal));
            if (sourceRemote == null || targetRemote == null)
                return false;

            if (PushFirst && NeedsPush)
            {
                using var lockWatcher = _repo.LockWatcher();
                ProgressDescription = $"Push {Source.Name} -> {sourceRemote.Name} ...";

                var log = _repo.CreateLog("Push");
                Use(log);

                var remoteBranch = _upstream ?? new Models.Branch() { Name = Source.Name, Remote = sourceRemote.Name };
                var succ = await new Commands.Push(
                    _repo.FullPath,
                    Source,
                    sourceRemote,
                    remoteBranch,
                    false,
                    _repo.Submodules.Count > 0,
                    _upstream == null,
                    false,
                    false).Use(log).ExecAsync();

                log.Complete();
                if (!succ)
                    return false;
            }

            if (_isPlatformSelectable)
            {
                _repo.Settings.PullRequestPlatforms[targetRemote.Name] = _selectedPlatform.Type;
                _repo.Settings.Save();
            }

            if (!targetRemote.TryGetVisitURL(out var targetURL) || !sourceRemote.TryGetVisitURL(out var sourceURL))
                return false;

            var sourceBranch = _upstream?.Name ?? Source.Name;
            var url = _selectedPlatform.BuildCreateURL(targetURL, sourceURL, sourceBranch, _selectedTarget.Name, _title, _description, out var contentDropped);
            if (contentDropped)
            {
                var content = string.IsNullOrEmpty(_description) ? _title : $"{_title}\n\n{_description}";
                await App.CopyTextAsync(content);
                _repo.SendNotification(App.Text("CreatePR.CopiedToClipboard"));
            }

            Native.OS.OpenBrowser(url);
            return true;
        }

        private Models.Remote GetDefaultPushRemote(Models.Branch target)
        {
            var remotes = _repo.Remotes;
            if (remotes.Count == 0)
                return null;

            if (!string.IsNullOrEmpty(_repo.Settings.DefaultRemote))
            {
                var preferred = remotes.Find(x => x.Name.Equals(_repo.Settings.DefaultRemote, StringComparison.Ordinal));
                if (preferred != null)
                    return preferred;
            }

            // Prefer a remote other than the target's (usually the fork of current user).
            if (target != null)
            {
                var other = remotes.Find(x => !x.Name.Equals(target.Remote, StringComparison.Ordinal));
                if (other != null)
                    return other;

                return remotes.Find(x => x.Name.Equals(target.Remote, StringComparison.Ordinal));
            }

            return remotes[0];
        }

        private void RefreshTargetBranches()
        {
            var host = GetHost(_selectedPushRemote);
            var branches = new List<Models.Branch>();
            foreach (var b in _repo.Branches)
            {
                if (b.IsLocal || b == _upstream)
                    continue;

                var remote = _repo.Remotes.Find(x => x.Name.Equals(b.Remote, StringComparison.Ordinal));
                if (host == null || host.Equals(GetHost(remote), StringComparison.OrdinalIgnoreCase))
                    branches.Add(b);
            }

            TargetBranches = branches;

            if (_selectedTarget == null)
                return;

            if (!branches.Contains(_selectedTarget))
                SelectedTarget = null;
            else
                ValidateProperty(_selectedTarget, nameof(SelectedTarget));
        }

        private void SelectDefaultTarget()
        {
            var remote = _selectedPushRemote?.Name;
            if (string.IsNullOrEmpty(remote))
                return;

            Task.Run(async () =>
            {
                var head = await new Commands.QueryRemoteHead(_repo.FullPath, remote).GetResultAsync().ConfigureAwait(false);
                Dispatcher.UIThread.Post(() =>
                {
                    if (_selectedTarget != null)
                        return;

                    string[] candidates = string.IsNullOrEmpty(head) ? ["main", "master", "develop"] : [head];
                    foreach (var name in candidates)
                    {
                        var found = _targetBranches.Find(x => x.Remote.Equals(remote, StringComparison.Ordinal) && x.Name.Equals(name, StringComparison.Ordinal));
                        if (found != null)
                        {
                            SelectedTarget = found;
                            return;
                        }
                    }
                });
            });
        }

        private void PostTargetChanged()
        {
            var targetRemote = _selectedTarget != null ? _repo.Remotes.Find(x => x.Name.Equals(_selectedTarget.Remote, StringComparison.Ordinal)) : null;
            if (targetRemote != null)
            {
                IsPlatformSelectable = targetRemote.TryGetVisitURL(out var url) &&
                    Models.PullRequestPlatform.Detect(new Uri(url).Host) == Models.PullRequestPlatformType.None;
                SelectedPlatform = Models.PullRequestPlatform.Resolve(targetRemote, _repo.Settings);
            }

            _commits = [];
            _commitsVersion++;

            if (_selectedTarget == null)
            {
                Templates = [];
                UpdateDefaultContent();
                return;
            }

            var version = _commitsVersion;
            var target = _selectedTarget.FullName;
            var source = Source.FullName;
            var repo = _repo.FullPath;
            Task.Run(async () =>
            {
                var commits = await new Commands.QueryCommits(repo, $"--no-merges -n 200 {target}..{source}", false).GetResultAsync().ConfigureAwait(false);
                var singleCommitMessage = commits.Count == 1 ? await new Commands.QueryCommitFullMessage(repo, commits[0].SHA).GetResultAsync().ConfigureAwait(false) : null;
                var templates = await new Commands.QueryPullRequestTemplates(repo, target).GetResultAsync().ConfigureAwait(false);
                if (templates.Count > 0)
                    templates.Insert(0, new Models.PullRequestTemplate() { Name = App.Text("CreatePR.NoTemplate") });

                Dispatcher.UIThread.Post(() =>
                {
                    if (version != _commitsVersion)
                        return;

                    commits.Reverse();
                    _commits = commits;
                    _singleCommitMessage = singleCommitMessage;

                    var selected = _selectedTemplate != null ? templates.Find(x => x.Name.Equals(_selectedTemplate.Name, StringComparison.Ordinal)) : null;
                    selected ??= templates.Count > 1 ? templates[1] : null;

                    Templates = templates;
                    _selectedTemplate = selected;
                    OnPropertyChanged(nameof(SelectedTemplate));
                    UpdateDefaultContent();
                });
            });
        }

        private void UpdateDefaultContent()
        {
            if (!_isTitleEdited)
            {
                SetProperty(ref _title, _commits.Count == 1 ? _commits[0].Subject : HumanizeBranchName(Source.Name), nameof(Title));
                ValidateProperty(_title, nameof(Title));
            }

            if (_isDescriptionEdited)
                return;

            var builder = new StringBuilder();
            if (!string.IsNullOrEmpty(_selectedTemplate?.Content))
                builder.Append(_selectedTemplate.Content).Append("\n\n");

            if (_commits.Count == 1)
            {
                // Body of the only commit (without its subject).
                var msg = _singleCommitMessage ?? string.Empty;
                var idx = msg.IndexOf('\n');
                if (idx > 0)
                    builder.Append(msg.Substring(idx + 1).Trim());
            }
            else if (_commits.Count > 1)
            {
                builder.Append("## Commits\n\n");
                foreach (var c in _commits)
                    builder.Append("- ").Append(c.Subject).Append('\n');
            }

            _generatedDescription = builder.ToString().Trim();
            SetProperty(ref _description, _generatedDescription, nameof(Description));
        }

        private static string HumanizeBranchName(string name)
        {
            var idx = name.LastIndexOf('/');
            if (idx >= 0)
                name = name.Substring(idx + 1);

            name = name.Replace('-', ' ').Replace('_', ' ').Trim();
            if (name.Length == 0)
                return name;

            return char.ToUpper(name[0], CultureInfo.CurrentCulture) + name.Substring(1);
        }

        private static string GetHost(Models.Remote remote)
        {
            if (remote == null || !remote.TryGetVisitURL(out var url))
                return null;

            return new Uri(url).Host;
        }

        private readonly Repository _repo = null;
        private readonly Models.Branch _upstream = null;
        private Models.Remote _selectedPushRemote = null;
        private List<Models.Branch> _targetBranches = [];
        private Models.Branch _selectedTarget = null;
        private bool _isPlatformSelectable = false;
        private Models.PullRequestPlatform _selectedPlatform = null;
        private List<Models.PullRequestTemplate> _templates = [];
        private Models.PullRequestTemplate _selectedTemplate = null;
        private List<Models.Commit> _commits = [];
        private string _singleCommitMessage = null;
        private int _commitsVersion = 0;
        private string _title = string.Empty;
        private string _description = string.Empty;
        private string _generatedDescription = string.Empty;
        private bool _isTitleEdited = false;
        private bool _isDescriptionEdited = false;
    }
}
