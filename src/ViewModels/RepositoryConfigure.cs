using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class RepositoryConfigure : ObservableObject
    {
        public AvaloniaList<Models.GitAccount> GitAccounts => GitAccountStore.Instance.Accounts;

        public Models.GitAccount SelectedGitAccount
        {
            get => _selectedGitAccount;
            set
            {
                if (!SetProperty(ref _selectedGitAccount, value))
                    return;

                if (value != null)
                {
                    _selectedGitAccountWasAppliedDuringSession = true;
                    ApplyGitAccount(value);
                }
            }
        }

        public string UserName
        {
            get => _userName;
            set
            {
                if (SetProperty(ref _userName, value) && !_applyingGitAccount)
                    ClearSelectedGitAccount();
            }
        }

        public string UserEmail
        {
            get => _userEmail;
            set
            {
                if (SetProperty(ref _userEmail, value) && !_applyingGitAccount)
                    ClearSelectedGitAccount();
            }
        }

        public List<string> Remotes
        {
            get;
        }

        public string DefaultRemote
        {
            get => _repo.Settings.DefaultRemote;
            set
            {
                if (_repo.Settings.DefaultRemote != value)
                {
                    _repo.Settings.DefaultRemote = value;
                    OnPropertyChanged();
                }
            }
        }

        public int PreferredMergeMode
        {
            get => _repo.Settings.PreferredMergeMode;
            set
            {
                if (_repo.Settings.PreferredMergeMode != value)
                {
                    _repo.Settings.PreferredMergeMode = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool GPGCommitSigningEnabled
        {
            get;
            set;
        }

        public bool GPGTagSigningEnabled
        {
            get;
            set;
        }

        public string GPGUserSigningKey
        {
            get;
            set;
        }

        public string HttpProxy
        {
            get => _httpProxy;
            set => SetProperty(ref _httpProxy, value);
        }

        public string ConventionalTypesOverride
        {
            get => _repo.Settings.ConventionalTypesOverride;
            set
            {
                if (_repo.Settings.ConventionalTypesOverride != value)
                {
                    _repo.Settings.ConventionalTypesOverride = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EnablePruneOnFetch
        {
            get;
            set;
        }

        public bool AskBeforeAutoUpdatingSubmodules
        {
            get => _repo.Settings.AskBeforeAutoUpdatingSubmodules;
            set => _repo.Settings.AskBeforeAutoUpdatingSubmodules = value;
        }

        public bool EnableRecursiveWhenAutoUpdatingSubmodules
        {
            get => _repo.Settings.EnableRecursiveWhenAutoUpdatingSubmodules;
            set => _repo.Settings.EnableRecursiveWhenAutoUpdatingSubmodules = value;
        }

        public AvaloniaList<Models.CommitTemplate> CommitTemplates
        {
            get => _repo.Settings.CommitTemplates;
        }

        public Models.CommitTemplate SelectedCommitTemplate
        {
            get => _selectedCommitTemplate;
            set => SetProperty(ref _selectedCommitTemplate, value);
        }

        public AvaloniaList<Models.IssueTracker> IssueTrackers
        {
            get;
        } = [];

        public Models.IssueTracker SelectedIssueTracker
        {
            get => _selectedIssueTracker;
            set => SetProperty(ref _selectedIssueTracker, value);
        }

        public List<string> AvailableOpenAIServices
        {
            get;
            private set;
        }

        public string PreferredOpenAIService
        {
            get => _repo.Settings.PreferredOpenAIService;
            set => _repo.Settings.PreferredOpenAIService = value;
        }

        public AvaloniaList<Models.CustomAction> CustomActions
        {
            get => _repo.Settings.CustomActions;
        }

        public Models.CustomAction SelectedCustomAction
        {
            get => _selectedCustomAction;
            set => SetProperty(ref _selectedCustomAction, value);
        }

        public RepositoryConfigure(Repository repo)
        {
            _repo = repo;

            Remotes = new List<string>();
            foreach (var remote in _repo.Remotes)
                Remotes.Add(remote.Name);

            AvailableOpenAIServices = new List<string>() { "---" };
            foreach (var service in Preferences.Instance.OpenAIServices)
                AvailableOpenAIServices.Add(service.Name);

            if (!AvailableOpenAIServices.Contains(PreferredOpenAIService))
                PreferredOpenAIService = "---";

            var config = new Commands.Config(repo.FullPath);
            _cached = config.ReadAll();
            if (_cached.TryGetValue("user.name", out var name))
                _userName = name;
            if (_cached.TryGetValue("user.email", out var email))
                _userEmail = email;
            if (_cached.TryGetValue("commit.gpgsign", out var gpgCommitSign))
                GPGCommitSigningEnabled = gpgCommitSign == "true";
            if (_cached.TryGetValue("tag.gpgsign", out var gpgTagSign))
                GPGTagSigningEnabled = gpgTagSign == "true";
            if (_cached.TryGetValue("user.signingkey", out var signingKey))
                GPGUserSigningKey = signingKey;
            if (_cached.TryGetValue("http.proxy", out var proxy))
                HttpProxy = proxy;
            if (_cached.TryGetValue("fetch.prune", out var prune))
                EnablePruneOnFetch = (prune == "true");

            var configuredGitAccountId = config.GetLocal(GitAccountConfigKey);
            _hadManagedGitAccount = !string.IsNullOrWhiteSpace(configuredGitAccountId);
            ResolveSelectedGitAccount(configuredGitAccountId);

            foreach (var rule in _repo.IssueTrackers)
            {
                IssueTrackers.Add(new()
                {
                    IsShared = rule.IsShared,
                    Name = rule.Name,
                    RegexString = rule.RegexString,
                    URLTemplate = rule.URLTemplate,
                });
            }
        }

        public void RefreshGitAccounts()
        {
            if (_selectedGitAccount != null && GitAccounts.Contains(_selectedGitAccount))
            {
                ApplyGitAccount(_selectedGitAccount);
                return;
            }

            ResolveSelectedGitAccount(null);
            OnPropertyChanged(nameof(SelectedGitAccount));
        }

        public void ClearHttpProxy()
        {
            HttpProxy = string.Empty;
        }

        public void AddCommitTemplate()
        {
            var template = new Models.CommitTemplate() { Name = "New Template" };
            _repo.Settings.CommitTemplates.Add(template);
            SelectedCommitTemplate = template;
        }

        public void RemoveSelectedCommitTemplate()
        {
            if (_selectedCommitTemplate != null)
                _repo.Settings.CommitTemplates.Remove(_selectedCommitTemplate);
            SelectedCommitTemplate = null;
        }

        public List<string> GetRemoteVisitUrls()
        {
            var outs = new List<string>();
            foreach (var remote in _repo.Remotes)
            {
                if (remote.TryGetVisitURL(out var url))
                    outs.Add(url);
            }
            return outs;
        }

        public void AddIssueTracker(string name, string regex, string url)
        {
            var rule = new Models.IssueTracker()
            {
                IsShared = false,
                Name = name,
                RegexString = regex,
                URLTemplate = url,
            };

            IssueTrackers.Add(rule);
            SelectedIssueTracker = rule;
        }

        public void RemoveIssueTracker()
        {
            if (_selectedIssueTracker is { } rule)
                IssueTrackers.Remove(rule);

            SelectedIssueTracker = null;
        }

        public void AddNewCustomAction()
        {
            SelectedCustomAction = _repo.Settings.AddNewCustomAction();
        }

        public void RemoveSelectedCustomAction()
        {
            _repo.Settings.RemoveCustomAction(_selectedCustomAction);
            SelectedCustomAction = null;
        }

        public void MoveSelectedCustomActionUp()
        {
            if (_selectedCustomAction != null)
                _repo.Settings.MoveCustomActionUp(_selectedCustomAction);
        }

        public void MoveSelectedCustomActionDown()
        {
            if (_selectedCustomAction != null)
                _repo.Settings.MoveCustomActionDown(_selectedCustomAction);
        }

        public async Task SaveAsync()
        {
            _repo.Settings.Save();

            var config = new Commands.Config(_repo.FullPath);
            if (_selectedGitAccount != null)
            {
                await config.SetAsync(GitAccountConfigKey, _selectedGitAccount.Id);
                await config.SetAsync("user.name", _selectedGitAccount.GitUserName);
                await config.SetAsync("user.email", _selectedGitAccount.GitEmail);

                if (HasGitHubHttpsRemote())
                {
                    await config.SetAsync(
                        "credential.username",
                        _selectedGitAccount.GitHubUserName);
                }
                else if (_hadManagedGitAccount)
                {
                    await config.SetAsync("credential.username", string.Empty);
                }
            }
            else
            {
                await SetIfChangedAsync("user.name", UserName, "");
                await SetIfChangedAsync("user.email", UserEmail, "");

                if (_hadManagedGitAccount || _selectedGitAccountWasAppliedDuringSession)
                {
                    await config.SetAsync(GitAccountConfigKey, string.Empty);
                    await config.SetAsync("credential.username", string.Empty);
                }
            }

            await SetIfChangedAsync("commit.gpgsign", GPGCommitSigningEnabled ? "true" : "false", "false");
            await SetIfChangedAsync("tag.gpgsign", GPGTagSigningEnabled ? "true" : "false", "false");
            await SetIfChangedAsync("user.signingkey", GPGUserSigningKey, "");
            await SetIfChangedAsync("http.proxy", HttpProxy, "");
            await SetIfChangedAsync("fetch.prune", EnablePruneOnFetch ? "true" : "false", "false");

            await ApplyIssueTrackerChangesAsync();
        }

        private void ApplyGitAccount(Models.GitAccount account)
        {
            _applyingGitAccount = true;
            UserName = account.GitUserName;
            UserEmail = account.GitEmail;
            _applyingGitAccount = false;
        }

        private void ClearSelectedGitAccount()
        {
            if (_selectedGitAccount == null)
                return;

            _selectedGitAccount = null;
            OnPropertyChanged(nameof(SelectedGitAccount));
        }

        private void ResolveSelectedGitAccount(string configuredId)
        {
            _selectedGitAccount = Models.GitAccountResolver.Resolve(
                GitAccounts,
                configuredId,
                _userName,
                _userEmail);
        }

        private bool HasGitHubHttpsRemote()
        {
            foreach (var remote in _repo.Remotes)
            {
                if (!Uri.TryCreate(remote.URL, UriKind.Absolute, out var uri))
                    continue;

                if ((uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                     uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)) &&
                    uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private async Task SetIfChangedAsync(string key, string value, string defValue)
        {
            if (value != _cached.GetValueOrDefault(key, defValue))
                await new Commands.Config(_repo.FullPath).SetAsync(key, value);
        }

        private async Task ApplyIssueTrackerChangesAsync()
        {
            var changed = false;
            var oldRules = new Dictionary<string, Models.IssueTracker>();
            foreach (var rule in _repo.IssueTrackers)
                oldRules.Add(rule.Name, rule);

            foreach (var rule in IssueTrackers)
            {
                if (oldRules.TryGetValue(rule.Name, out var old))
                {
                    if (old.IsShared != rule.IsShared)
                    {
                        changed = true;
                        await new Commands.IssueTracker(_repo.FullPath, old.IsShared).RemoveAsync(old.Name);
                        await new Commands.IssueTracker(_repo.FullPath, rule.IsShared).AddAsync(rule);
                    }
                    else
                    {
                        if (!old.RegexString.Equals(rule.RegexString, StringComparison.Ordinal))
                        {
                            changed = true;
                            await new Commands.IssueTracker(_repo.FullPath, old.IsShared).UpdateRegexAsync(rule);
                        }

                        if (!old.URLTemplate.Equals(rule.URLTemplate, StringComparison.Ordinal))
                        {
                            changed = true;
                            await new Commands.IssueTracker(_repo.FullPath, old.IsShared).UpdateURLTemplateAsync(rule);
                        }
                    }

                    oldRules.Remove(rule.Name);
                }
                else
                {
                    changed = true;
                    await new Commands.IssueTracker(_repo.FullPath, rule.IsShared).AddAsync(rule);
                }
            }

            if (oldRules.Count > 0)
            {
                changed = true;

                foreach (var kv in oldRules)
                    await new Commands.IssueTracker(_repo.FullPath, kv.Value.IsShared).RemoveAsync(kv.Key);
            }

            if (changed)
            {
                _repo.IssueTrackers.Clear();
                _repo.IssueTrackers.AddRange(IssueTrackers);
            }
        }

        private const string GitAccountConfigKey = "sourcegit.account";

        private readonly Repository _repo;
        private readonly Dictionary<string, string> _cached;
        private string _userName = string.Empty;
        private string _userEmail = string.Empty;
        private string _httpProxy;
        private bool _applyingGitAccount;
        private bool _hadManagedGitAccount;
        private bool _selectedGitAccountWasAppliedDuringSession;
        private Models.GitAccount _selectedGitAccount = null;
        private Models.CommitTemplate _selectedCommitTemplate = null;
        private Models.IssueTracker _selectedIssueTracker = null;
        private Models.CustomAction _selectedCustomAction = null;
    }
}
