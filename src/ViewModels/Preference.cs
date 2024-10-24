using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class Preference : ObservableObject
    {
        [JsonIgnore]
        public static Preference Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                _isLoading = true;
                _instance = Load();
                _isLoading = false;

                _instance.PrepareGit();
                _instance.PrepareShellOrTerminal();
                _instance.PrepareWorkspaces();
                _instance.PrepareOpenAIPrompt();

                return _instance;
            }
        }

        public string Locale
        {
            get => _locale;
            set
            {
                if (SetProperty(ref _locale, value) && !_isLoading)
                    App.SetLocale(value);
            }
        }

        public string Theme
        {
            get => _theme;
            set
            {
                if (SetProperty(ref _theme, value) && !_isLoading)
                    App.SetTheme(_theme, _themeOverrides);
            }
        }

        public string ThemeOverrides
        {
            get => _themeOverrides;
            set
            {
                if (SetProperty(ref _themeOverrides, value) && !_isLoading)
                    App.SetTheme(_theme, value);
            }
        }

        public string DefaultFontFamily
        {
            get => _defaultFontFamily;
            set
            {
                if (SetProperty(ref _defaultFontFamily, value) && !_isLoading)
                    App.SetFonts(_defaultFontFamily, _monospaceFontFamily, _onlyUseMonoFontInEditor);
            }
        }

        public string MonospaceFontFamily
        {
            get => _monospaceFontFamily;
            set
            {
                if (SetProperty(ref _monospaceFontFamily, value) && !_isLoading)
                    App.SetFonts(_defaultFontFamily, _monospaceFontFamily, _onlyUseMonoFontInEditor);
            }
        }

        public bool OnlyUseMonoFontInEditor
        {
            get => _onlyUseMonoFontInEditor;
            set
            {
                if (SetProperty(ref _onlyUseMonoFontInEditor, value) && !_isLoading)
                    App.SetFonts(_defaultFontFamily, _monospaceFontFamily, _onlyUseMonoFontInEditor);
            }
        }

        public bool UseSystemWindowFrame
        {
            get => _useSystemWindowFrame;
            set => SetProperty(ref _useSystemWindowFrame, value);
        }

        public double DefaultFontSize
        {
            get => _defaultFontSize;
            set => SetProperty(ref _defaultFontSize, value);
        }

        public LayoutInfo Layout
        {
            get => _layout;
            set => SetProperty(ref _layout, value);
        }

        public int MaxHistoryCommits
        {
            get => _maxHistoryCommits;
            set => SetProperty(ref _maxHistoryCommits, value);
        }

        public int SubjectGuideLength
        {
            get => _subjectGuideLength;
            set => SetProperty(ref _subjectGuideLength, value);
        }

        public bool UseFixedTabWidth
        {
            get => _useFixedTabWidth;
            set => SetProperty(ref _useFixedTabWidth, value);
        }

        public bool Check4UpdatesOnStartup
        {
            get => _check4UpdatesOnStartup;
            set => SetProperty(ref _check4UpdatesOnStartup, value);
        }

        public bool ShowAuthorTimeInGraph
        {
            get => _showAuthorTimeInGraph;
            set => SetProperty(ref _showAuthorTimeInGraph, value);
        }

        public string IgnoreUpdateTag
        {
            get => _ignoreUpdateTag;
            set => SetProperty(ref _ignoreUpdateTag, value);
        }

        public bool ShowTagsAsTree
        {
            get => _showTagsAsTree;
            set => SetProperty(ref _showTagsAsTree, value);
        }

        public bool UseTwoColumnsLayoutInHistories
        {
            get => _useTwoColumnsLayoutInHistories;
            set => SetProperty(ref _useTwoColumnsLayoutInHistories, value);
        }

        public bool DisplayTimeAsPeriodInHistories
        {
            get => _displayTimeAsPeriodInHistories;
            set => SetProperty(ref _displayTimeAsPeriodInHistories, value);
        }

        public bool UseSideBySideDiff
        {
            get => _useSideBySideDiff;
            set => SetProperty(ref _useSideBySideDiff, value);
        }

        public bool UseSyntaxHighlighting
        {
            get => _useSyntaxHighlighting;
            set => SetProperty(ref _useSyntaxHighlighting, value);
        }

        public bool EnableDiffViewWordWrap
        {
            get => _enableDiffViewWordWrap;
            set => SetProperty(ref _enableDiffViewWordWrap, value);
        }

        public bool ShowHiddenSymbolsInDiffView
        {
            get => _showHiddenSymbolsInDiffView;
            set => SetProperty(ref _showHiddenSymbolsInDiffView, value);
        }

        public Models.ChangeViewMode UnstagedChangeViewMode
        {
            get => _unstagedChangeViewMode;
            set => SetProperty(ref _unstagedChangeViewMode, value);
        }

        public Models.ChangeViewMode StagedChangeViewMode
        {
            get => _stagedChangeViewMode;
            set => SetProperty(ref _stagedChangeViewMode, value);
        }

        public Models.ChangeViewMode CommitChangeViewMode
        {
            get => _commitChangeViewMode;
            set => SetProperty(ref _commitChangeViewMode, value);
        }

        public string GitInstallPath
        {
            get => Native.OS.GitExecutable;
            set
            {
                if (Native.OS.GitExecutable != value)
                {
                    Native.OS.GitExecutable = value;
                    OnPropertyChanged();
                }
            }
        }

        public string GitDefaultCloneDir
        {
            get => _gitDefaultCloneDir;
            set => SetProperty(ref _gitDefaultCloneDir, value);
        }

        public int ShellOrTerminal
        {
            get => _shellOrTerminal;
            set
            {
                if (SetProperty(ref _shellOrTerminal, value))
                {
                    if (value >= 0 && value < Models.ShellOrTerminal.Supported.Count)
                        Native.OS.SetShellOrTerminal(Models.ShellOrTerminal.Supported[value]);
                    else
                        Native.OS.SetShellOrTerminal(null);

                    OnPropertyChanged(nameof(ShellOrTerminalPath));
                }
            }
        }

        public string ShellOrTerminalPath
        {
            get => Native.OS.ShellOrTerminal;
            set
            {
                if (value != Native.OS.ShellOrTerminal)
                {
                    Native.OS.ShellOrTerminal = value;
                    OnPropertyChanged();
                }
            }
        }

        public int ExternalMergeToolType
        {
            get => _externalMergeToolType;
            set
            {
                var changed = SetProperty(ref _externalMergeToolType, value);
                if (changed && !OperatingSystem.IsWindows() && value > 0 && value < Models.ExternalMerger.Supported.Count)
                {
                    var tool = Models.ExternalMerger.Supported[value];
                    if (File.Exists(tool.Exec))
                        ExternalMergeToolPath = tool.Exec;
                    else
                        ExternalMergeToolPath = string.Empty;
                }
            }
        }

        public string ExternalMergeToolPath
        {
            get => _externalMergeToolPath;
            set => SetProperty(ref _externalMergeToolPath, value);
        }

        public string OpenAIServer
        {
            get => Models.OpenAI.Server;
            set
            {
                if (value != Models.OpenAI.Server)
                {
                    Models.OpenAI.Server = value;
                    OnPropertyChanged();
                }
            }
        }

        public string OpenAIApiKey
        {
            get => Models.OpenAI.ApiKey;
            set
            {
                if (value != Models.OpenAI.ApiKey)
                {
                    Models.OpenAI.ApiKey = value;
                    OnPropertyChanged();
                }
            }
        }

        public string OpenAIModel
        {
            get => Models.OpenAI.Model;
            set
            {
                if (value != Models.OpenAI.Model)
                {
                    Models.OpenAI.Model = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SelectedLanguage
        {
            get => Models.OpenAI.SelectedLanguage;
            set
            {
                if (value != Models.OpenAI.SelectedLanguage)
                {
                    Models.OpenAI.SelectedLanguage = value;
                    OnPropertyChanged();
                }
            }
        }

        public string OpenAIAnalyzeDiffPrompt
        {
            get => Models.OpenAI.AnalyzeDiffPrompt;
            set
            {
                if (value != Models.OpenAI.AnalyzeDiffPrompt)
                {
                    Models.OpenAI.AnalyzeDiffPrompt = value;
                    OnPropertyChanged();
                }
            }
        }

        public string OpenAIGenerateSubjectPrompt
        {
            get => Models.OpenAI.GenerateSubjectPrompt;
            set
            {
                if (value != Models.OpenAI.GenerateSubjectPrompt)
                {
                    Models.OpenAI.GenerateSubjectPrompt = value;
                    OnPropertyChanged();
                }
            }
        }

        public uint StatisticsSampleColor
        {
            get => _statisticsSampleColor;
            set => SetProperty(ref _statisticsSampleColor, value);
        }

        public List<RepositoryNode> RepositoryNodes
        {
            get;
            set;
        } = [];

        public List<Workspace> Workspaces
        {
            get;
            set;
        } = [];

        public double LastCheckUpdateTime
        {
            get => _lastCheckUpdateTime;
            set => SetProperty(ref _lastCheckUpdateTime, value);
        }

        public bool IsGitConfigured()
        {
            var path = GitInstallPath;
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }

        public bool ShouldCheck4UpdateOnStartup()
        {
            if (!_check4UpdatesOnStartup)
                return false;

            var lastCheck = DateTime.UnixEpoch.AddSeconds(LastCheckUpdateTime).ToLocalTime();
            var now = DateTime.Now;

            if (lastCheck.Year == now.Year && lastCheck.Month == now.Month && lastCheck.Day == now.Day)
                return false;

            LastCheckUpdateTime = now.Subtract(DateTime.UnixEpoch.ToLocalTime()).TotalSeconds;
            return true;
        }

        public Workspace GetActiveWorkspace()
        {
            foreach (var w in Workspaces)
            {
                if (w.IsActive)
                    return w;
            }

            var first = Workspaces[0];
            first.IsActive = true;
            return first;
        }

        public void AddNode(RepositoryNode node, RepositoryNode to, bool save)
        {
            var collection = to == null ? RepositoryNodes : to.SubNodes;
            collection.Add(node);
            collection.Sort((l, r) =>
            {
                if (l.IsRepository != r.IsRepository)
                    return l.IsRepository ? 1 : -1;

                return string.Compare(l.Name, r.Name, StringComparison.Ordinal);
            });

            if (save)
                Save();
        }

        public RepositoryNode FindNode(string id)
        {
            return FindNodeRecursive(id, RepositoryNodes);
        }

        public RepositoryNode FindOrAddNodeByRepositoryPath(string repo, RepositoryNode parent, bool shouldMoveNode)
        {
            var node = FindNodeRecursive(repo, RepositoryNodes);
            if (node == null)
            {
                node = new RepositoryNode()
                {
                    Id = repo,
                    Name = Path.GetFileName(repo),
                    Bookmark = 0,
                    IsRepository = true,
                };

                AddNode(node, parent, true);
            }
            else if (shouldMoveNode)
            {
                MoveNode(node, parent, true);
            }

            return node;
        }

        public void MoveNode(RepositoryNode node, RepositoryNode to, bool save)
        {
            if (to == null && RepositoryNodes.Contains(node))
                return;
            if (to != null && to.SubNodes.Contains(node))
                return;

            RemoveNode(node, false);
            AddNode(node, to, false);

            if (save)
                Save();
        }

        public void RemoveNode(RepositoryNode node, bool save)
        {
            RemoveNodeRecursive(node, RepositoryNodes);

            if (save)
                Save();
        }

        public void SortByRenamedNode(RepositoryNode node)
        {
            var container = FindNodeContainer(node, RepositoryNodes);
            container?.Sort((l, r) =>
            {
                if (l.IsRepository != r.IsRepository)
                    return l.IsRepository ? 1 : -1;

                return string.Compare(l.Name, r.Name, StringComparison.Ordinal);
            });

            Save();
        }

        public void AutoRemoveInvalidNode()
        {
            var changed = RemoveInvalidRepositoriesRecursive(RepositoryNodes);
            if (changed)
                Save();
        }

        public void Save()
        {
            if (_isLoading)
                return;

            var file = Path.Combine(Native.OS.DataDir, "preference.json");
            var data = JsonSerializer.Serialize(this, JsonCodeGen.Default.Preference);
            File.WriteAllText(file, data);
        }

        private static Preference Load()
        {
            var path = Path.Combine(Native.OS.DataDir, "preference.json");
            if (!File.Exists(path))
                return new Preference();

            try
            {
                return JsonSerializer.Deserialize(File.ReadAllText(path), JsonCodeGen.Default.Preference);
            }
            catch
            {
                return new Preference();
            }
        }

        private void PrepareGit()
        {
            var path = Native.OS.GitExecutable;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                GitInstallPath = Native.OS.FindGitExecutable();
        }

        private void PrepareShellOrTerminal()
        {
            if (_shellOrTerminal >= 0)
                return;

            for (int i = 0; i < Models.ShellOrTerminal.Supported.Count; i++)
            {
                var shell = Models.ShellOrTerminal.Supported[i];
                if (Native.OS.TestShellOrTerminal(shell))
                {
                    ShellOrTerminal = i;
                    break;
                }
            }
        }

        private void PrepareWorkspaces()
        {
            if (Workspaces.Count == 0)
            {
                Workspaces.Add(new Workspace() { Name = "Default" });
                return;
            }

            foreach (var workspace in Workspaces)
            {
                if (!workspace.RestoreOnStartup)
                {
                    workspace.Repositories.Clear();
                    workspace.ActiveIdx = 0;
                }
            }
        }

        private void PrepareOpenAIPrompt()
        {
            if (string.IsNullOrEmpty(Models.OpenAI.AnalyzeDiffPrompt))
            {
                Models.OpenAI.AnalyzeDiffPrompt = """
                    You are an expert developer specialist in creating commits.
                    Provide a super concise one sentence overall changes summary of the user `git diff` output following strictly the next rules:
                    - Do not use any code snippets, imports, file routes or bullets points.
                    - Do not mention the route of file that has been change.
                    - Write clear, concise, and descriptive messages that explain the MAIN GOAL made of the changes.
                    - Use the present tense and active voice in the message, for example, "Fix bug" instead of "Fixed bug.".
                    - Use the imperative mood, which gives the message a sense of command, e.g. "Add feature" instead of "Added feature".
                    - Avoid using general terms like "update" or "change", be specific about what was updated or changed.
                    - Avoid using terms like "The main goal of", just output directly the summary in plain text
                    """;
            }

            if (string.IsNullOrEmpty(Models.OpenAI.GenerateSubjectPrompt))
            {
                Models.OpenAI.GenerateSubjectPrompt = """
                    You are an expert developer specialist in creating commits messages.
                    Your only goal is to retrieve a single commit message.
                    Based on the provided user changes, combine them in ONE SINGLE commit message retrieving the global idea, following strictly the next rules:
                    - Assign the commit {type} according to the next conditions:
                        feat: Only when adding a new feature.
                        fix: When fixing a bug.
                        docs: When updating documentation.
                        style: When changing elements styles or design and/or making changes to the code style (formatting, missing semicolons, etc.) without changing the code logic.
                        test: When adding or updating tests.
                        chore: When making changes to the build process or auxiliary tools and libraries.
                        revert: When undoing a previous commit.
                        refactor: When restructuring code without changing its external behavior, or is any of the other refactor types.
                    - Do not add any issues numeration, explain your output nor introduce your answer.
                    - Output directly only one commit message in plain text with the next format: {type}: {commit_message}.
                    - Be as concise as possible, keep the message under 50 characters.
                    """;
            }

            if (string.IsNullOrEmpty(Models.OpenAI.SelectedLanguage))
                Models.OpenAI.SelectedLanguage = "English";
        }

        private RepositoryNode FindNodeRecursive(string id, List<RepositoryNode> collection)
        {
            foreach (var node in collection)
            {
                if (node.Id == id)
                    return node;

                var sub = FindNodeRecursive(id, node.SubNodes);
                if (sub != null)
                    return sub;
            }

            return null;
        }

        private List<RepositoryNode> FindNodeContainer(RepositoryNode node, List<RepositoryNode> collection)
        {
            foreach (var sub in collection)
            {
                if (node == sub)
                    return collection;

                var subCollection = FindNodeContainer(node, sub.SubNodes);
                if (subCollection != null)
                    return subCollection;
            }

            return null;
        }

        private bool RemoveNodeRecursive(RepositoryNode node, List<RepositoryNode> collection)
        {
            if (collection.Contains(node))
            {
                collection.Remove(node);
                return true;
            }

            foreach (var one in collection)
            {
                if (RemoveNodeRecursive(node, one.SubNodes))
                    return true;
            }

            return false;
        }

        private bool RemoveInvalidRepositoriesRecursive(List<RepositoryNode> collection)
        {
            bool changed = false;

            for (int i = collection.Count - 1; i >= 0; i--)
            {
                var node = collection[i];
                if (node.IsInvalid)
                {
                    collection.RemoveAt(i);
                    changed = true;
                }
                else if (!node.IsRepository)
                {
                    changed |= RemoveInvalidRepositoriesRecursive(node.SubNodes);
                }
            }

            return changed;
        }

        private static Preference _instance = null;
        private static bool _isLoading = false;

        private string _locale = "en_US";
        private string _theme = "Default";
        private string _themeOverrides = string.Empty;
        private string _defaultFontFamily = string.Empty;
        private string _monospaceFontFamily = string.Empty;
        private bool _onlyUseMonoFontInEditor = false;
        private bool _useSystemWindowFrame = false;
        private double _defaultFontSize = 13;
        private LayoutInfo _layout = new LayoutInfo();

        private int _maxHistoryCommits = 20000;
        private int _subjectGuideLength = 50;
        private bool _useFixedTabWidth = true;
        private bool _showAuthorTimeInGraph = false;

        private bool _check4UpdatesOnStartup = true;
        private double _lastCheckUpdateTime = 0;
        private string _ignoreUpdateTag = string.Empty;

        private bool _showTagsAsTree = false;
        private bool _useTwoColumnsLayoutInHistories = false;
        private bool _displayTimeAsPeriodInHistories = false;
        private bool _useSideBySideDiff = false;
        private bool _useSyntaxHighlighting = false;
        private bool _enableDiffViewWordWrap = false;
        private bool _showHiddenSymbolsInDiffView = false;

        private Models.ChangeViewMode _unstagedChangeViewMode = Models.ChangeViewMode.List;
        private Models.ChangeViewMode _stagedChangeViewMode = Models.ChangeViewMode.List;
        private Models.ChangeViewMode _commitChangeViewMode = Models.ChangeViewMode.List;

        private string _gitDefaultCloneDir = string.Empty;

        private int _shellOrTerminal = -1;
        private int _externalMergeToolType = 0;
        private string _externalMergeToolPath = string.Empty;

        private uint _statisticsSampleColor = 0xFF00FF00;
    }
}
