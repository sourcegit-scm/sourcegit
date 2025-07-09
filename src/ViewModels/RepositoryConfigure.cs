﻿using System.Collections.Generic;
using System.Threading.Tasks;

using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class RepositoryConfigure : ObservableObject
    {
        public string UserName
        {
            get;
            set;
        }

        public string UserEmail
        {
            get;
            set;
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

        public bool EnablePruneOnFetch
        {
            get;
            set;
        }

        public bool EnableAutoFetch
        {
            get => _repo.Settings.EnableAutoFetch;
            set => _repo.Settings.EnableAutoFetch = value;
        }

        public int? AutoFetchInterval
        {
            get => _repo.Settings.AutoFetchInterval;
            set
            {
                if (value is null || value < 1)
                    return;

                var interval = (int)value;
                if (_repo.Settings.AutoFetchInterval != interval)
                    _repo.Settings.AutoFetchInterval = interval;
            }
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

        public AvaloniaList<Models.IssueTrackerRule> IssueTrackerRules
        {
            get => _repo.Settings.IssueTrackerRules;
        }

        public Models.IssueTrackerRule SelectedIssueTrackerRule
        {
            get => _selectedIssueTrackerRule;
            set => SetProperty(ref _selectedIssueTrackerRule, value);
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

            _cached = new Commands.Config(repo.FullPath).ReadAllAsync().Result;
            if (_cached.TryGetValue("user.name", out var name))
                UserName = name;
            if (_cached.TryGetValue("user.email", out var email))
                UserEmail = email;
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

        public void AddSampleGitHubIssueTracker()
        {
            var link = "https://github.com/username/repository/issues/$1";
            foreach (var remote in _repo.Remotes)
            {
                if (remote.URL.Contains("github.com", System.StringComparison.Ordinal) &&
                    remote.TryGetVisitURL(out string url))
                {
                    link = $"{url}/issues/$1";
                    break;
                }
            }

            SelectedIssueTrackerRule = _repo.Settings.AddIssueTracker("GitHub ISSUE", @"#(\d+)", link);
        }

        public void AddSampleJiraIssueTracker()
        {
            SelectedIssueTrackerRule = _repo.Settings.AddIssueTracker("Jira Tracker", @"PROJ-(\d+)", "https://jira.yourcompany.com/browse/PROJ-$1");
        }

        public void AddSampleAzureWorkItemTracker()
        {
            SelectedIssueTrackerRule = _repo.Settings.AddIssueTracker("Azure DevOps Tracker", @"#(\d+)", "https://dev.azure.com/yourcompany/workspace/_workitems/edit/$1");
        }

        public void AddSampleGitLabIssueTracker()
        {
            var link = "https://gitlab.com/username/repository/-/issues/$1";
            foreach (var remote in _repo.Remotes)
            {
                if (remote.TryGetVisitURL(out string url))
                {
                    link = $"{url}/-/issues/$1";
                    break;
                }
            }

            SelectedIssueTrackerRule = _repo.Settings.AddIssueTracker("GitLab ISSUE", @"#(\d+)", link);
        }

        public void AddSampleGitLabMergeRequestTracker()
        {
            var link = "https://gitlab.com/username/repository/-/merge_requests/$1";
            foreach (var remote in _repo.Remotes)
            {
                if (remote.TryGetVisitURL(out string url))
                {
                    link = $"{url}/-/merge_requests/$1";
                    break;
                }
            }

            SelectedIssueTrackerRule = _repo.Settings.AddIssueTracker("GitLab MR", @"!(\d+)", link);
        }

        public void AddSampleGiteeIssueTracker()
        {
            var link = "https://gitee.com/username/repository/issues/$1";
            foreach (var remote in _repo.Remotes)
            {
                if (remote.URL.Contains("gitee.com", System.StringComparison.Ordinal) &&
                    remote.TryGetVisitURL(out string url))
                {
                    link = $"{url}/issues/$1";
                    break;
                }
            }

            SelectedIssueTrackerRule = _repo.Settings.AddIssueTracker("Gitee ISSUE", @"#([0-9A-Z]{6,10})", link);
        }

        public void AddSampleGiteePullRequestTracker()
        {
            var link = "https://gitee.com/username/repository/pulls/$1";
            foreach (var remote in _repo.Remotes)
            {
                if (remote.URL.Contains("gitee.com", System.StringComparison.Ordinal) &&
                    remote.TryGetVisitURL(out string url))
                {
                    link = $"{url}/pulls/$1";
                }
            }

            SelectedIssueTrackerRule = _repo.Settings.AddIssueTracker("Gitee Pull Request", @"!(\d+)", link);
        }

        public void NewIssueTracker()
        {
            SelectedIssueTrackerRule = _repo.Settings.AddIssueTracker("New Issue Tracker", @"#(\d+)", "https://xxx/$1");
        }

        public void RemoveSelectedIssueTracker()
        {
            _repo.Settings.RemoveIssueTracker(_selectedIssueTrackerRule);
            SelectedIssueTrackerRule = null;
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
            await SetIfChangedAsync("user.name", UserName, "");
            await SetIfChangedAsync("user.email", UserEmail, "");
            await SetIfChangedAsync("commit.gpgsign", GPGCommitSigningEnabled ? "true" : "false", "false");
            await SetIfChangedAsync("tag.gpgsign", GPGTagSigningEnabled ? "true" : "false", "false");
            await SetIfChangedAsync("user.signingkey", GPGUserSigningKey, "");
            await SetIfChangedAsync("http.proxy", HttpProxy, "");
            await SetIfChangedAsync("fetch.prune", EnablePruneOnFetch ? "true" : "false", "false");
        }

        private async Task SetIfChangedAsync(string key, string value, string defValue)
        {
            if (value != _cached.GetValueOrDefault(key, defValue))
                await new Commands.Config(_repo.FullPath).SetAsync(key, value);
        }

        private readonly Repository _repo = null;
        private readonly Dictionary<string, string> _cached = null;
        private string _httpProxy;
        private Models.CommitTemplate _selectedCommitTemplate = null;
        private Models.IssueTrackerRule _selectedIssueTrackerRule = null;
        private Models.CustomAction _selectedCustomAction = null;
    }
}
