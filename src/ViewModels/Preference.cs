using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using Avalonia.Collections;
using Avalonia.Media;

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
                if (_instance == null)
                {
                    if (!File.Exists(_savePath))
                    {
                        _instance = new Preference();
                    }
                    else
                    {
                        try
                        {
                            _instance = JsonSerializer.Deserialize(File.ReadAllText(_savePath), JsonCodeGen.Default.Preference);
                        }
                        catch
                        {
                            _instance = new Preference();
                        }
                    }
                }

                if (_instance.DefaultFont == null)
                    _instance.DefaultFont = FontManager.Current.DefaultFontFamily;

                if (_instance.MonospaceFont == null)
                    _instance.MonospaceFont = new FontFamily("fonts:SourceGit#JetBrains Mono");

                if (!_instance.IsGitConfigured)
                    _instance.GitInstallPath = Native.OS.FindGitExecutable();

                return _instance;
            }
        }

        public string Locale
        {
            get => _locale;
            set
            {
                if (SetProperty(ref _locale, value))
                    App.SetLocale(value);
            }
        }

        public string Theme
        {
            get => _theme;
            set
            {
                if (SetProperty(ref _theme, value))
                    App.SetTheme(_theme, _themeOverrides);
            }
        }

        public string ThemeOverrides
        {
            get => _themeOverrides;
            set
            {
                if (SetProperty(ref _themeOverrides, value))
                    App.SetTheme(_theme, value);
            }
        }

        [JsonConverter(typeof(FontFamilyConverter))]
        public FontFamily DefaultFont
        {
            get => _defaultFont;
            set => SetProperty(ref _defaultFont, value);
        }

        [JsonConverter(typeof(FontFamilyConverter))]
        public FontFamily MonospaceFont
        {
            get => _monospaceFont;
            set => SetProperty(ref _monospaceFont, value);
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

        public string AvatarServer
        {
            get => Models.AvatarManager.SelectedServer;
            set
            {
                if (Models.AvatarManager.SelectedServer != value)
                {
                    Models.AvatarManager.SelectedServer = value;
                    OnPropertyChanged(nameof(AvatarServer));
                }
            }
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

        public bool RestoreTabs
        {
            get => _restoreTabs;
            set => SetProperty(ref _restoreTabs, value);
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

        public string IgnoreUpdateTag
        {
            get;
            set;
        } = string.Empty;

        public bool UseTwoColumnsLayoutInHistories
        {
            get => _useTwoColumnsLayoutInHistories;
            set => SetProperty(ref _useTwoColumnsLayoutInHistories, value);
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

        public int DiffViewVisualLineNumbers
        {
            get => _diffViewVisualLineNumbers;
            set => SetProperty(ref _diffViewVisualLineNumbers, value);
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

        [JsonIgnore]
        public bool IsGitConfigured
        {
            get => !string.IsNullOrEmpty(GitInstallPath) && File.Exists(GitInstallPath);
        }

        public string GitInstallPath
        {
            get => Native.OS.GitExecutable;
            set
            {
                if (Native.OS.GitExecutable != value)
                {
                    Native.OS.GitExecutable = value;
                    OnPropertyChanged(nameof(GitInstallPath));
                }
            }
        }

        public Models.Shell GitShell
        {
            get => Native.OS.GetShell();
            set
            {
                if (Native.OS.SetShell(value))
                {
                    OnPropertyChanged(nameof(GitShell));
                }
            }
        }

        public string GitDefaultCloneDir
        {
            get => _gitDefaultCloneDir;
            set => SetProperty(ref _gitDefaultCloneDir, value);
        }

        public bool GitAutoFetch
        {
            get => Commands.AutoFetch.IsEnabled;
            set
            {
                if (Commands.AutoFetch.IsEnabled != value)
                {
                    Commands.AutoFetch.IsEnabled = value;
                    OnPropertyChanged(nameof(GitAutoFetch));
                }
            }
        }

        public int? GitAutoFetchInterval
        {
            get => Commands.AutoFetch.Interval;
            set
            {
                if (value is null or < 1)
                    return;

                if (Commands.AutoFetch.Interval != value)
                {
                    Commands.AutoFetch.Interval = (int)value;
                    OnPropertyChanged(nameof(GitAutoFetchInterval));
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

        public AvaloniaList<RepositoryNode> RepositoryNodes
        {
            get => _repositoryNodes;
            set => SetProperty(ref _repositoryNodes, value);
        }

        public List<string> OpenedTabs
        {
            get;
            set;
        } = new List<string>();

        public int LastActiveTabIdx
        {
            get;
            set;
        } = 0;

        public double LastCheckUpdateTime
        {
            get;
            set;
        } = 0;

        [JsonIgnore]
        public bool ShouldCheck4UpdateOnStartup
        {
            get
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
        }

        public static void AddNode(RepositoryNode node, RepositoryNode to = null)
        {
            var collection = to == null ? _instance._repositoryNodes : to.SubNodes;
            var list = new List<RepositoryNode>();
            list.AddRange(collection);
            list.Add(node);
            list.Sort((l, r) =>
            {
                if (l.IsRepository != r.IsRepository)
                    return l.IsRepository ? 1 : -1;
                else
                    return l.Name.CompareTo(r.Name);
            });

            collection.Clear();
            foreach (var one in list)
                collection.Add(one);
        }

        public static RepositoryNode FindNode(string id)
        {
            return FindNodeRecursive(id, _instance.RepositoryNodes);
        }

        public static RepositoryNode FindOrAddNodeByRepositoryPath(string repo, RepositoryNode parent, bool shouldMoveNode)
        {
            var node = FindNodeRecursive(repo, _instance.RepositoryNodes);
            if (node == null)
            {
                node = new RepositoryNode()
                {
                    Id = repo,
                    Name = Path.GetFileName(repo),
                    Bookmark = 0,
                    IsRepository = true,
                };

                AddNode(node, parent);
            }
            else if (shouldMoveNode)
            {
                MoveNode(node, parent);
            }

            return node;
        }

        public static void MoveNode(RepositoryNode node, RepositoryNode to = null)
        {
            if (to == null && _instance._repositoryNodes.Contains(node))
                return;
            if (to != null && to.SubNodes.Contains(node))
                return;

            RemoveNode(node);
            AddNode(node, to);
        }

        public static void RemoveNode(RepositoryNode node)
        {
            RemoveNodeRecursive(node, _instance._repositoryNodes);
        }

        public static void SortByRenamedNode(RepositoryNode node)
        {
            var container = FindNodeContainer(node, _instance._repositoryNodes);
            if (container == null)
                return;

            var list = new List<RepositoryNode>();
            list.AddRange(container);
            list.Sort((l, r) =>
            {
                if (l.IsRepository != r.IsRepository)
                {
                    return l.IsRepository ? 1 : -1;
                }
                else
                {
                    return l.Name.CompareTo(r.Name);
                }
            });

            container.Clear();
            foreach (var one in list)
                container.Add(one);
        }

        public void Save()
        {
            var data = JsonSerializer.Serialize(this, JsonCodeGen.Default.Preference);
            File.WriteAllText(_savePath, data);
        }

        private static RepositoryNode FindNodeRecursive(string id, AvaloniaList<RepositoryNode> collection)
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

        private static AvaloniaList<RepositoryNode> FindNodeContainer(RepositoryNode node, AvaloniaList<RepositoryNode> collection)
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

        private static bool RemoveNodeRecursive(RepositoryNode node, AvaloniaList<RepositoryNode> collection)
        {
            if (collection.Contains(node))
            {
                collection.Remove(node);
                return true;
            }

            foreach (RepositoryNode one in collection)
            {
                if (RemoveNodeRecursive(node, one.SubNodes))
                    return true;
            }

            return false;
        }

        private static Preference _instance = null;
        private static readonly string _savePath = Path.Combine(Native.OS.DataDir, "preference.json");

        private string _locale = "en_US";
        private string _theme = "Default";
        private string _themeOverrides = string.Empty;
        private FontFamily _defaultFont = null;
        private FontFamily _monospaceFont = null;
        private double _defaultFontSize = 13;
        private LayoutInfo _layout = new LayoutInfo();

        private int _maxHistoryCommits = 20000;
        private int _subjectGuideLength = 50;
        private bool _restoreTabs = false;
        private bool _useFixedTabWidth = true;
        private bool _check4UpdatesOnStartup = true;
        
        private bool _useTwoColumnsLayoutInHistories = false;
        private bool _useSideBySideDiff = false;
        private bool _useSyntaxHighlighting = false;
        private bool _enableDiffViewWordWrap = false;
        private bool _showHiddenSymbolsInDiffView = false;
        private int _diffViewVisualLineNumbers = 4;

        private Models.ChangeViewMode _unstagedChangeViewMode = Models.ChangeViewMode.List;
        private Models.ChangeViewMode _stagedChangeViewMode = Models.ChangeViewMode.List;
        private Models.ChangeViewMode _commitChangeViewMode = Models.ChangeViewMode.List;

        private string _gitDefaultCloneDir = string.Empty;

        private int _externalMergeToolType = 0;
        private string _externalMergeToolPath = string.Empty;

        private AvaloniaList<RepositoryNode> _repositoryNodes = new AvaloniaList<RepositoryNode>();
    }

    public class FontFamilyConverter : JsonConverter<FontFamily>
    {
        public override FontFamily Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var name = reader.GetString();
            return new FontFamily(name);
        }

        public override void Write(Utf8JsonWriter writer, FontFamily value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
