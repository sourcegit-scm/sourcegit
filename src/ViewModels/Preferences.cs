using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class Preferences : ObservableObject
    {
        [JsonIgnore]
        public static Preferences Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                _instance = Load();
                _instance._isLoading = false;

                _instance.PrepareGit();
                _instance.PrepareShellOrTerminal();
                _instance.PrepareWorkspaces();

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
                    App.SetFonts(value, _monospaceFontFamily);
            }
        }

        public string MonospaceFontFamily
        {
            get => _monospaceFontFamily;
            set
            {
                if (SetProperty(ref _monospaceFontFamily, value) && !_isLoading)
                    App.SetFonts(_defaultFontFamily, value);
            }
        }

        public bool UseSystemWindowFrame
        {
            get => Native.OS.UseSystemWindowFrame;
            set => Native.OS.UseSystemWindowFrame = value;
        }

        public double DefaultFontSize
        {
            get => _defaultFontSize;
            set => SetProperty(ref _defaultFontSize, value);
        }

        public double EditorFontSize
        {
            get => _editorFontSize;
            set => SetProperty(ref _editorFontSize, value);
        }

        public int EditorTabWidth
        {
            get => _editorTabWidth;
            set => SetProperty(ref _editorTabWidth, value);
        }

        public LayoutInfo Layout
        {
            get => _layout;
            set => SetProperty(ref _layout, value);
        }

        public bool ShowLocalChangesByDefault
        {
            get;
            set;
        } = false;

        public bool ShowChangesInCommitDetailByDefault
        {
            get;
            set;
        } = false;

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

        public int DateTimeFormat
        {
            get => Models.DateTimeFormat.ActiveIndex;
            set
            {
                if (value != Models.DateTimeFormat.ActiveIndex &&
                    value >= 0 &&
                    value < Models.DateTimeFormat.Supported.Count)
                {
                    Models.DateTimeFormat.ActiveIndex = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool UseAutoHideScrollBars
        {
            get => _useAutoHideScrollBars;
            set => SetProperty(ref _useAutoHideScrollBars, value);
        }

        public bool UseGitHubStyleAvatar
        {
            get => _useGitHubStyleAvatar;
            set => SetProperty(ref _useGitHubStyleAvatar, value);
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

        public bool ShowChildren
        {
            get => _showChildren;
            set => SetProperty(ref _showChildren, value);
        }

        public string IgnoreUpdateTag
        {
            get => _ignoreUpdateTag;
            set => SetProperty(ref _ignoreUpdateTag, value);
        }

        public bool ShowTagsAsTree
        {
            get;
            set;
        } = false;

        public bool ShowTagsInGraph
        {
            get => _showTagsInGraph;
            set => SetProperty(ref _showTagsInGraph, value);
        }

        public bool ShowSubmodulesAsTree
        {
            get;
            set;
        } = false;

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

        public bool IgnoreCRAtEOLInDiff
        {
            get => Models.DiffOption.IgnoreCRAtEOL;
            set
            {
                if (Models.DiffOption.IgnoreCRAtEOL != value)
                {
                    Models.DiffOption.IgnoreCRAtEOL = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IgnoreWhitespaceChangesInDiff
        {
            get => _ignoreWhitespaceChangesInDiff;
            set => SetProperty(ref _ignoreWhitespaceChangesInDiff, value);
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

        public bool UseFullTextDiff
        {
            get => _useFullTextDiff;
            set => SetProperty(ref _useFullTextDiff, value);
        }

        public int LFSImageActiveIdx
        {
            get => _lfsImageActiveIdx;
            set => SetProperty(ref _lfsImageActiveIdx, value);
        }

        public int ImageDiffActiveIdx
        {
            get => _imageDiffActiveIdx;
            set => SetProperty(ref _imageDiffActiveIdx, value);
        }

        public bool EnableCompactFoldersInChangesTree
        {
            get => _enableCompactFoldersInChangesTree;
            set => SetProperty(ref _enableCompactFoldersInChangesTree, value);
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

        public Models.ChangeViewMode StashChangeViewMode
        {
            get => _stashChangeViewMode;
            set => SetProperty(ref _stashChangeViewMode, value);
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

        public bool UseLibsecretInsteadOfGCM
        {
            get => Native.OS.CredentialHelper.Equals("libsecret", StringComparison.Ordinal);
            set
            {
                var helper = value ? "libsecret" : "manager";
                if (OperatingSystem.IsLinux() && !Native.OS.CredentialHelper.Equals(helper, StringComparison.Ordinal))
                {
                    Native.OS.CredentialHelper = helper;
                    OnPropertyChanged();
                }
            }
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
            get => Native.OS.ExternalMergerType;
            set
            {
                if (Native.OS.ExternalMergerType != value)
                {
                    Native.OS.ExternalMergerType = value;
                    OnPropertyChanged();

                    if (!_isLoading)
                    {
                        Native.OS.AutoSelectExternalMergeToolExecFile();
                        OnPropertyChanged(nameof(ExternalMergeToolPath));
                    }
                }
            }
        }

        public string ExternalMergeToolPath
        {
            get => Native.OS.ExternalMergerExecFile;
            set
            {
                if (!Native.OS.ExternalMergerExecFile.Equals(value, StringComparison.Ordinal))
                {
                    Native.OS.ExternalMergerExecFile = value;
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

        public AvaloniaList<Models.CustomAction> CustomActions
        {
            get;
            set;
        } = [];

        public AvaloniaList<Models.OpenAIService> OpenAIServices
        {
            get;
            set;
        } = [];

        public double LastCheckUpdateTime
        {
            get => _lastCheckUpdateTime;
            set => SetProperty(ref _lastCheckUpdateTime, value);
        }

        public void SetCanModify()
        {
            _isReadonly = false;
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
            SortNodes(collection);

            if (save)
                Save();
        }

        public void SortNodes(List<RepositoryNode> collection)
        {
            collection?.Sort((l, r) =>
            {
                if (l.IsRepository != r.IsRepository)
                    return l.IsRepository ? 1 : -1;

                return Models.NumericSort.Compare(l.Name, r.Name);
            });
        }

        public RepositoryNode FindNode(string id)
        {
            return FindNodeRecursive(id, RepositoryNodes);
        }

        public RepositoryNode FindOrAddNodeByRepositoryPath(string repo, RepositoryNode parent, bool shouldMoveNode, bool save = true)
        {
            var normalized = repo.Replace('\\', '/').TrimEnd('/');

            var node = FindNodeRecursive(normalized, RepositoryNodes);
            if (node == null)
            {
                node = new RepositoryNode()
                {
                    Id = normalized,
                    Name = Path.GetFileName(normalized),
                    Bookmark = 0,
                    IsRepository = true,
                };

                AddNode(node, parent, save);
            }
            else if (shouldMoveNode)
            {
                MoveNode(node, parent, save);
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
            SortNodes(container);
            Save();
        }

        public void AutoRemoveInvalidNode()
        {
            RemoveInvalidRepositoriesRecursive(RepositoryNodes);
        }

        public void Save()
        {
            if (_isLoading || _isReadonly)
                return;

            var file = Path.Combine(Native.OS.DataDir, "preference.json");
            using var stream = File.Create(file);
            JsonSerializer.Serialize(stream, this, JsonCodeGen.Default.Preferences);
        }

        private static Preferences Load()
        {
            var path = Path.Combine(Native.OS.DataDir, "preference.json");
            if (!File.Exists(path))
                return new Preferences();

            try
            {
                using var stream = File.OpenRead(path);
                return JsonSerializer.Deserialize(stream, JsonCodeGen.Default.Preferences);
            }
            catch
            {
                return new Preferences();
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

        private static Preferences _instance = null;

        private bool _isLoading = true;
        private bool _isReadonly = true;
        private string _locale = "en_US";
        private string _theme = "Default";
        private string _themeOverrides = string.Empty;
        private string _defaultFontFamily = string.Empty;
        private string _monospaceFontFamily = string.Empty;
        private double _defaultFontSize = 13;
        private double _editorFontSize = 13;
        private int _editorTabWidth = 4;
        private LayoutInfo _layout = new();

        private int _maxHistoryCommits = 20000;
        private int _subjectGuideLength = 50;
        private bool _useAutoHideScrollBars = true;
        private bool _useGitHubStyleAvatar = true;
        private bool _showAuthorTimeInGraph = false;
        private bool _showChildren = false;

        private bool _check4UpdatesOnStartup = true;
        private double _lastCheckUpdateTime = 0;
        private string _ignoreUpdateTag = string.Empty;

        private bool _showTagsInGraph = true;
        private bool _useTwoColumnsLayoutInHistories = false;
        private bool _displayTimeAsPeriodInHistories = false;
        private bool _useSideBySideDiff = false;
        private bool _ignoreWhitespaceChangesInDiff = false;
        private bool _useSyntaxHighlighting = false;
        private bool _enableDiffViewWordWrap = false;
        private bool _showHiddenSymbolsInDiffView = false;
        private bool _useFullTextDiff = false;
        private int _lfsImageActiveIdx = 0;
        private int _imageDiffActiveIdx = 0;
        private bool _enableCompactFoldersInChangesTree = false;

        private Models.ChangeViewMode _unstagedChangeViewMode = Models.ChangeViewMode.List;
        private Models.ChangeViewMode _stagedChangeViewMode = Models.ChangeViewMode.List;
        private Models.ChangeViewMode _commitChangeViewMode = Models.ChangeViewMode.List;
        private Models.ChangeViewMode _stashChangeViewMode = Models.ChangeViewMode.List;

        private string _gitDefaultCloneDir = string.Empty;
        private int _shellOrTerminal = -1;
        private uint _statisticsSampleColor = 0xFF00FF00;
    }
}
