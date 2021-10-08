using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SourceGit.Models {

    /// <summary>
    ///     程序配置
    /// </summary>
    public class Preference {
        private static readonly string SAVE_PATH = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SourceGit",
            "preference_v4.json");
        private static Preference instance = null;

        /// <summary>
        ///     通用配置
        /// </summary>
        public class GeneralInfo {

            /// <summary>
            ///     显示语言
            /// </summary>
            public string Locale { get; set; } = "en_US";

            /// <summary>
            ///     系统字体
            /// </summary>
            public string FontFamilyWindowSetting { get; set; } = "Microsoft YaHei UI";

            [JsonIgnore]
            public string FontFamilyWindow {
                get => FontFamilyWindowSetting + ",Microsoft YaHei UI";
                set => FontFamilyWindowSetting = value;
            }

            /// <summary>
            ///     用户字体（提交列表、提交日志、差异比较等）
            /// </summary>
            public string FontFamilyContentSetting { get; set; } = "Consolas";

            [JsonIgnore] public string FontFamilyContent {
                get => FontFamilyContentSetting + ",Microsoft YaHei UI";
                set => FontFamilyContentSetting = value;
            }

            /// <summary>
            ///     头像服务器
            /// </summary>
            public string AvatarServer { get; set; } = "https://www.gravatar.com/avatar/";

            /// <summary>
            ///     是否启用深色主题
            /// </summary>
            public bool UseDarkTheme { get; set; } = true;

            /// <summary>
            ///     启用更新检测
            /// </summary>
            public bool CheckForUpdate { get; set; } = true;

            /// <summary>
            ///     上一次检测的时间（用于控制每天仅第一次启动软件时，检测）
            /// </summary>
            public int LastCheckDay { get; set; } = 0;

            /// <summary>
            ///     启用自动拉取远程变更（每10分钟一次）
            /// </summary>
            public bool AutoFetchRemotes { get; set; } = true;

            /// <summary>
            ///     是否启用崩溃上报
            /// </summary>
            public bool EnableCrashReport { get; set; } = false;

            /// <summary>
            ///     是否尝试使用 Windows Terminal 打开终端
            /// </summary>
            public bool UseWindowsTerminal { get; set; } = false;
        }

        /// <summary>
        ///     Git配置
        /// </summary>
        public class GitInfo {

            /// <summary>
            ///     git.exe所在路径
            /// </summary>
            public string Path { get; set; }

            /// <summary>
            ///     默认克隆路径
            /// </summary>
            public string DefaultCloneDir { get; set; }
        }

        /// <summary>
        ///     外部合并工具配置
        /// </summary>
        public class MergeToolInfo {
            /// <summary>
            ///     合并工具类型
            /// </summary>
            public int Type { get; set; } = 0;

            /// <summary>
            ///     合并工具可执行文件路径
            /// </summary>
            public string Path { get; set; } = "";
        }

        /// <summary>
        ///     使用设置
        /// </summary>
        public class WindowInfo {

            /// <summary>
            ///     最近一次设置的宽度
            /// </summary>
            public double Width { get; set; } = 800;

            /// <summary>
            ///     最近一次设置的高度
            /// </summary>
            public double Height { get; set; } = 600;

            /// <summary>
            ///     将提交信息面板与提交记录左右排布
            /// </summary>
            public bool MoveCommitInfoRight { get; set; } = false;

            /// <summary>
            ///     使用合并Diff视图
            /// </summary>
            public bool UseCombinedDiff { get; set; } = false;

            /// <summary>
            ///     未暂存视图中变更显示方式
            /// </summary>
            public Change.DisplayMode ChangeInUnstaged { get; set; } = Change.DisplayMode.Tree;

            /// <summary>
            ///     暂存视图中变更显示方式
            /// </summary>
            public Change.DisplayMode ChangeInStaged { get; set; } = Change.DisplayMode.Tree;

            /// <summary>
            ///     提交信息视图中变更显示方式
            /// </summary>
            public Change.DisplayMode ChangeInCommitInfo { get; set; } = Change.DisplayMode.Tree;
        }

        /// <summary>
        ///     恢复上次打开的窗口
        /// </summary>
        public class RestoreTabs {

            /// <summary>
            ///     是否开启该功能
            /// </summary>
            public bool IsEnabled { get; set; } = false;

            /// <summary>
            ///     上次打开的仓库
            /// </summary>
            public List<string> Opened { get; set; } = new List<string>();

            /// <summary>
            ///     最后浏览的仓库
            /// </summary>
            public string Actived { get; set; } = null;
        }

        /// <summary>
        ///     全局配置
        /// </summary>
        [JsonIgnore]
        public static Preference Instance {
            get {
                if (instance == null) return Load();
                return instance;
            }
        }

        /// <summary>
        ///     检测配置是否正常
        /// </summary>
        [JsonIgnore]
        public bool IsReady {
            get => File.Exists(Git.Path) && new Commands.Version().Query() != null;
        }

        #region DATA
        public GeneralInfo General { get; set; } = new GeneralInfo();
        public GitInfo Git { get; set; } = new GitInfo();
        public MergeToolInfo MergeTool { get; set; } = new MergeToolInfo();
        public WindowInfo Window { get; set; } = new WindowInfo();
        public List<Group> Groups { get; set; } = new List<Group>();
        public List<Repository> Repositories { get; set; } = new List<Repository>();
        public List<string> Recents { get; set; } = new List<string>();
        public RestoreTabs Restore { get; set; } = new RestoreTabs();
        #endregion

        #region LOAD_SAVE
        public static Preference Load() {
            if (!File.Exists(SAVE_PATH)) {
                instance = new Preference();
            } else {
                try {
                    instance = JsonSerializer.Deserialize<Preference>(File.ReadAllText(SAVE_PATH));
                } catch {
                    instance = new Preference();
                }
            }

            if (!instance.IsReady) {
                var reg = RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine,
                    Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32);
                var git = reg.OpenSubKey("SOFTWARE\\GitForWindows");
                if (git != null) {
                    instance.Git.Path = Path.Combine(git.GetValue("InstallPath") as string, "bin", "git.exe");
                }
            }

            return instance;
        }

        public static void Save() {
            var dir = Path.GetDirectoryName(SAVE_PATH);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var data = JsonSerializer.Serialize(instance, new JsonSerializerOptions() { WriteIndented = true });
            File.WriteAllText(SAVE_PATH, data);
        }
        #endregion

        #region METHOD_ON_GROUPS
        public Group AddGroup(string name, string parentId) {
            var group = new Group() {
                Name = name,
                Id = Guid.NewGuid().ToString(),
                Parent = parentId,
                IsExpanded = false,
            };

            Groups.Add(group);
            Groups.Sort((l, r) => l.Name.CompareTo(r.Name));

            return group;
        }

        public Group FindGroup(string id) {
            foreach (var group in Groups) {
                if (group.Id == id) return group;
            }
            return null;
        }

        public void RenameGroup(string id, string newName) {
            foreach (var group in Groups) {
                if (group.Id == id) {
                    group.Name = newName;
                    break;
                }
            }

            Groups.Sort((l, r) => l.Name.CompareTo(r.Name));
        }

        public void RemoveGroup(string id) {
            int removedIdx = -1;

            for (int i = 0; i < Groups.Count; i++) {
                if (Groups[i].Id == id) {
                    removedIdx = i;
                    break;
                }
            }

            if (removedIdx >= 0) Groups.RemoveAt(removedIdx);
        }

        public bool IsSubGroup(string parent, string subId) {
            if (string.IsNullOrEmpty(parent)) return false;
            if (parent == subId) return true;

            var g = FindGroup(subId);
            if (g == null) return false;

            g = FindGroup(g.Parent);
            while (g != null) {
                if (g.Id == parent) return true;
                g = FindGroup(g.Parent);
            }

            return false;
        }
        #endregion

        #region METHOD_ON_REPOSITORIES
        public Repository AddRepository(string path, string gitDir, string groupId) {
            var repo = FindRepository(path);
            if (repo != null) return repo;

            var dir = new DirectoryInfo(path);
            repo = new Repository() {
                Path = dir.FullName,
                GitDir = gitDir,
                Name = dir.Name,
                GroupId = groupId,
            };

            Repositories.Add(repo);
            Repositories.Sort((l, r) => l.Name.CompareTo(r.Name));
            return repo;
        }

        public Repository FindRepository(string path) {
            var dir = new DirectoryInfo(path);
            foreach (var repo in Repositories) {
                if (repo.Path == dir.FullName) return repo;
            }
            return null;
        }

        public void RenameRepository(string path, string newName) {
            var repo = FindRepository(path);
            if (repo == null) return;

            repo.Name = newName;
            Repositories.Sort((l, r) => l.Name.CompareTo(r.Name));
        }

        public void RemoveRepository(string path) {
            var dir = new DirectoryInfo(path);
            var removedIdx = -1;

            for (int i = 0; i < Repositories.Count; i++) {
                if (Repositories[i].Path == dir.FullName) {
                    removedIdx = i;
                    break;
                }
            }

            if (removedIdx >= 0) Repositories.RemoveAt(removedIdx);
        }
        #endregion

        #region RECENTS
        public void AddRecent(string path) {
            if (Recents.Count == 0) {
                Recents.Add(path);
                return;
            }

            for (int i = 0; i < Recents.Count; i++) {
                if (Recents[i] == path) {
                    if (i != 0) {
                        Recents.RemoveAt(i);
                        Recents.Insert(0, path);
                    }

                    return;
                }
            }

            Recents.Insert(0, path);
        }

        public void RemoveRecent(string path) {
            for (int i = 0; i < Recents.Count; i++) {
                if (Recents[i] == path) {
                    Recents.RemoveAt(i);
                    return;
                }
            }
        }
        #endregion
    }
}