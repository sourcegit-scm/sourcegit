using System.Collections.Generic;
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

        public bool EnableSignOffForCommit
        {
            get => _repo.Settings.EnableSignOffForCommit;
            set => _repo.Settings.EnableSignOffForCommit = value;
        }

        public bool EnablePruneOnFetch
        {
            get => _repo.Settings.EnablePruneOnFetch;
            set => _repo.Settings.EnablePruneOnFetch = value;
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

        public string PreferedOpenAIService
        {
            get => _repo.Settings.PreferedOpenAIService;
            set => _repo.Settings.PreferedOpenAIService = value;
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
            foreach (var service in Preference.Instance.OpenAIServices)
                AvailableOpenAIServices.Add(service.Name);

            if (AvailableOpenAIServices.IndexOf(PreferedOpenAIService) == -1)
                PreferedOpenAIService = "---";

            _cached = new Commands.Config(repo.FullPath).ListAll();
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

        public void AddSampleGithubIssueTracker()
        {
            foreach (var remote in _repo.Remotes)
            {
                if (remote.URL.Contains("github.com", System.StringComparison.Ordinal))
                {
                    if (remote.TryGetVisitURL(out string url))
                    {
                        SelectedIssueTrackerRule = _repo.Settings.AddGithubIssueTracker(url);
                        return;
                    }
                }
            }

            SelectedIssueTrackerRule = _repo.Settings.AddGithubIssueTracker(null);
        }

        public void AddSampleJiraIssueTracker()
        {
            SelectedIssueTrackerRule = _repo.Settings.AddJiraIssueTracker();
        }

        public void AddSampleGitLabIssueTracker()
        {
            foreach (var remote in _repo.Remotes)
            {
                if (remote.TryGetVisitURL(out string url))
                {
                    SelectedIssueTrackerRule = _repo.Settings.AddGitLabIssueTracker(url);
                    return;
                }
            }

            SelectedIssueTrackerRule = _repo.Settings.AddGitLabIssueTracker(null);
        }

        public void AddSampleGitLabMergeRequestTracker()
        {
            foreach (var remote in _repo.Remotes)
            {
                if (remote.TryGetVisitURL(out string url))
                {
                    SelectedIssueTrackerRule = _repo.Settings.AddGitLabMergeRequestTracker(url);
                    return;
                }
            }

            SelectedIssueTrackerRule = _repo.Settings.AddGitLabMergeRequestTracker(null);
        }

        public void NewIssueTracker()
        {
            SelectedIssueTrackerRule = _repo.Settings.AddNewIssueTracker();
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

        public void Save()
        {
            SetIfChanged("user.name", UserName, "");
            SetIfChanged("user.email", UserEmail, "");
            SetIfChanged("commit.gpgsign", GPGCommitSigningEnabled ? "true" : "false", "false");
            SetIfChanged("tag.gpgsign", GPGTagSigningEnabled ? "true" : "false", "false");
            SetIfChanged("user.signingkey", GPGUserSigningKey, "");
            SetIfChanged("http.proxy", HttpProxy, "");
        }

        private void SetIfChanged(string key, string value, string defValue)
        {
            bool changed = false;
            if (_cached.TryGetValue(key, out var old))
            {
                changed = old != value;
            }
            else if (!string.IsNullOrEmpty(value) && value != defValue)
            {
                changed = true;
            }

            if (changed)
            {
                new Commands.Config(_repo.FullPath).Set(key, value);
            }
        }

        private readonly Repository _repo = null;
        private readonly Dictionary<string, string> _cached = null;
        private string _httpProxy;
        private Models.CommitTemplate _selectedCommitTemplate = null;
        private Models.IssueTrackerRule _selectedIssueTrackerRule = null;
        private Models.CustomAction _selectedCustomAction = null;
    }
}
