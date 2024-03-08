using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SourceGit.ViewModels {
    public class Preference : ObservableObject {
        [JsonIgnore]
        public static Preference Instance {
            get {
                if (_instance == null) {
                    if (!File.Exists(_savePath)) {
                        _instance = new Preference();
                    } else {
                        try {
                            _instance = JsonSerializer.Deserialize(File.ReadAllText(_savePath), JsonSerializationCodeGen.Default.Preference);
                        } catch {
                            _instance = new Preference();
                        }
                    }
                }

                _instance.Repositories.RemoveAll(x => !Directory.Exists(x.FullPath));

                if (!_instance.IsGitConfigured) {
                    _instance.GitInstallPath = Native.OS.FindGitExecutable();
                }

                return _instance;
            }
        }

        public string Locale {
            get => _locale;
            set {
                if (SetProperty(ref _locale, value)) {
                    App.SetLocale(value);
                }
            }
        }

        public string Theme {
            get => _theme;
            set {
                if (SetProperty(ref _theme, value)) {
                    App.SetTheme(value);
                }
            }
        }

        public string AvatarServer {
            get => Models.AvatarManager.SelectedServer;
            set {
                if (Models.AvatarManager.SelectedServer != value) {
                    Models.AvatarManager.SelectedServer = value;
                    OnPropertyChanged(nameof(AvatarServer));
                }
            }
        }

        public int MaxHistoryCommits {
            get => _maxHistoryCommits;
            set => SetProperty(ref _maxHistoryCommits, value);
        }

        public bool RestoreTabs {
            get => _restoreTabs;
            set => SetProperty(ref _restoreTabs, value);
        }

        public bool UseFixedTabWidth {
            get => _useFixedTabWidth;
            set => SetProperty(ref _useFixedTabWidth, value);
        }

        public bool UseTwoColumnsLayoutInHistories {
            get => _useTwoColumnsLayoutInHistories;
            set => SetProperty(ref _useTwoColumnsLayoutInHistories, value);
        }

        public bool UseCombinedTextDiff {
            get => _useCombinedTextDiff;
            set => SetProperty(ref _useCombinedTextDiff, value);
        }

        public Models.ChangeViewMode UnstagedChangeViewMode {
            get => _unstagedChangeViewMode;
            set => SetProperty(ref _unstagedChangeViewMode, value);
        }

        public Models.ChangeViewMode StagedChangeViewMode {
            get => _stagedChangeViewMode;
            set => SetProperty(ref _stagedChangeViewMode, value);
        }

        public Models.ChangeViewMode CommitChangeViewMode {
            get => _commitChangeViewMode;
            set => SetProperty(ref _commitChangeViewMode, value);
        }

        [JsonIgnore]
        public bool IsGitConfigured {
            get => !string.IsNullOrEmpty(GitInstallPath) && File.Exists(GitInstallPath);
        }

        public string GitInstallPath {
            get => Native.OS.GitInstallPath;
            set {
                if (Native.OS.GitInstallPath != value) {
                    Native.OS.GitInstallPath = value;
                    OnPropertyChanged(nameof(GitInstallPath));
                }
            }
        }

        public string GitDefaultCloneDir {
            get => _gitDefaultCloneDir;
            set => SetProperty(ref _gitDefaultCloneDir, value);
        }

        public bool GitAutoFetch {
            get => Commands.AutoFetch.IsEnabled;
            set {
                if (Commands.AutoFetch.IsEnabled != value) {
                    Commands.AutoFetch.IsEnabled = value;
                    OnPropertyChanged(nameof(GitAutoFetch));
                }
            }
        }

        public int ExternalMergeToolType {
            get => _externalMergeToolType;
            set {
                var changed = SetProperty(ref _externalMergeToolType, value);
                if (changed && !OperatingSystem.IsWindows() && value > 0 && value < Models.ExternalMergeTools.Supported.Count) {
                    var tool = Models.ExternalMergeTools.Supported[value];
                    if (File.Exists(tool.Exec)) ExternalMergeToolPath = tool.Exec;
                    else ExternalMergeToolPath = string.Empty;
                }
            }
        }

        public string ExternalMergeToolPath {
            get => _externalMergeToolPath;
            set => SetProperty(ref _externalMergeToolPath, value);
        }

        public string ExternalMergeToolCmd {
            get => _externalMergeToolCmd;
            set => SetProperty(ref _externalMergeToolCmd, value);
        }

        public string ExternalMergeToolDiffCmd {
            get => _externalMergeToolDiffCmd;
            set => SetProperty(ref _externalMergeToolDiffCmd, value);
        }

        public List<Repository> Repositories {
            get;
            set;
        } = new List<Repository>();

        public AvaloniaList<RepositoryNode> RepositoryNodes {
            get => _repositoryNodes;
            set => SetProperty(ref _repositoryNodes, value);
        }

        public List<string> OpenedTabs {
            get;
            set;
        } = new List<string>();

        public int LastActiveTabIdx {
            get;
            set;
        } = 0;

        public static void AddNode(RepositoryNode node, RepositoryNode to = null) {
            var collection = to == null ? _instance._repositoryNodes : to.SubNodes;
            var list = new List<RepositoryNode>();
            list.AddRange(collection);
            list.Add(node);
            list.Sort((l, r) => {
                if (l.IsRepository != r.IsRepository) {
                    return l.IsRepository ? 1 : -1;
                } else {
                    return l.Name.CompareTo(r.Name);
                }
            });

            collection.Clear();
            foreach (var one in list) {
                collection.Add(one);
            }
        }

        public static RepositoryNode FindNode(string id) {
            return FindNodeRecursive(id, _instance.RepositoryNodes);
        }

        public static void MoveNode(RepositoryNode node, RepositoryNode to = null) {
            if (to == null && _instance._repositoryNodes.Contains(node)) return;
            if (to != null && to.SubNodes.Contains(node)) return;

            RemoveNode(node);
            AddNode(node, to);
        }

        public static void RemoveNode(RepositoryNode node) {
            RemoveNodeRecursive(node, _instance._repositoryNodes);
        }

        public static Repository FindRepository(string path) {
            foreach (var repo in _instance.Repositories) {
                if (repo.FullPath == path) return repo;
            }
            return null;
        }

        public static Repository AddRepository(string rootDir, string gitDir) {
            var normalized = rootDir.Replace('\\', '/');
            var repo = FindRepository(normalized);
            if (repo != null) {
                repo.GitDir = gitDir;
                return repo;
            }

            repo = new Repository() {
                FullPath = normalized,
                GitDir = gitDir
            };

            _instance.Repositories.Add(repo);
            return repo;
        }

        public static void Save() {
            var dir = Path.GetDirectoryName(_savePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var data = JsonSerializer.Serialize(_instance, JsonSerializationCodeGen.Default.Preference);
            File.WriteAllText(_savePath, data);
        }

        private static RepositoryNode FindNodeRecursive(string id, AvaloniaList<RepositoryNode> collection) {
            foreach (var node in collection) {
                if (node.Id == id) return node;

                var sub = FindNodeRecursive(id, node.SubNodes);
                if (sub != null) return sub;
            }

            return null;
        }

        private static bool RemoveNodeRecursive(RepositoryNode node, AvaloniaList<RepositoryNode> collection) {
            if (collection.Contains(node)) {
                collection.Remove(node);
                return true;
            }

            foreach (RepositoryNode one in collection) {
                if (RemoveNodeRecursive(node, one.SubNodes)) return true;
            }

            return false;
        }

        private static Preference _instance = null;
        private static readonly string _savePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SourceGit",
            "preference.json");

        private string _locale = "en_US";
        private string _theme = "Default";
        private int _maxHistoryCommits = 20000;
        private bool _restoreTabs = false;
        private bool _useFixedTabWidth = true;
        private bool _useTwoColumnsLayoutInHistories = false;
        private bool _useCombinedTextDiff = true;

        private Models.ChangeViewMode _unstagedChangeViewMode = Models.ChangeViewMode.List;
        private Models.ChangeViewMode _stagedChangeViewMode = Models.ChangeViewMode.List;
        private Models.ChangeViewMode _commitChangeViewMode = Models.ChangeViewMode.List;

        private string _gitDefaultCloneDir = string.Empty;

        private int _externalMergeToolType = 0;
        private string _externalMergeToolPath = string.Empty;
        private string _externalMergeToolCmd = string.Empty;
        private string _externalMergeToolDiffCmd = string.Empty;

        private AvaloniaList<RepositoryNode> _repositoryNodes = new AvaloniaList<RepositoryNode>();
    }

    [JsonSourceGenerationOptions(WriteIndented = true, IgnoreReadOnlyFields = true, IgnoreReadOnlyProperties = true)]
    [JsonSerializable(typeof(Preference))]
    internal partial class JsonSerializationCodeGen : JsonSerializerContext { }
}