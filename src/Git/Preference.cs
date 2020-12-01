using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SourceGit.Git {

    /// <summary>
    ///     User's preference settings. Serialized to 
    /// </summary>
    public class Preference {

        /// <summary>
        ///     Group(Virtual folder) for watched repositories.
        /// </summary>
        public class Group {
            /// <summary>
            ///     Unique ID of this group.
            /// </summary>
            public string Id { get; set; }
            /// <summary>
            ///     Display name.
            /// </summary>
            public string Name { get; set; }
            /// <summary>
            ///     Parent ID.
            /// </summary>
            public string ParentId { get; set; }
            /// <summary>
            ///     Cache UI IsExpended status.
            /// </summary>
            public bool IsExpended { get; set; }
        }

        /// <summary>
        ///     File's display mode.
        /// </summary>
        public enum FilesDisplayMode {
            Tree,
            List,
            Grid,
        }

        #region STATICS
        /// <summary>
        ///     Storage path for Preference.
        /// </summary>
        private static readonly string SAVE_PATH = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SourceGit",
            "preference.json");
        /// <summary>
        ///     Runtime singleton instance.
        /// </summary>
        private static Preference instance = null;
        public static Preference Instance {
            get {
                if (instance == null) Load();
                return instance;
            }
            set {
                instance = value;
            }
        }
        #endregion

        #region SETTING_GENERAL
        /// <summary>
        ///     Use light color theme.
        /// </summary>
        public bool UseLightTheme { get; set; }
        /// <summary>
        ///     Check for updates.
        /// </summary>
        public bool CheckUpdate { get; set; }
        #endregion

        #region SETTING_GIT
        /// <summary>
        ///     Git executable file path.
        /// </summary>
        public string GitExecutable { get; set; }
        /// <summary>
        ///     Default clone directory.
        /// </summary>
        public string GitDefaultCloneDir { get; set; }
        #endregion

        #region SETTING_MERGE_TOOL
        /// <summary>
        ///     Selected merge tool.
        /// </summary>
        public int MergeTool { get; set; } = 0;
        /// <summary>
        ///     Executable file path for merge tool.
        /// </summary>
        public string MergeExecutable { get; set; } = "--";
        #endregion

        #region SETTING_UI
        /// <summary>
        ///     Main window's width
        /// </summary>
        public double UIMainWindowWidth { get; set; }
        /// <summary>
        ///     Main window's height
        /// </summary>
        public double UIMainWindowHeight { get; set; }
        /// <summary>
        ///     Show/Hide tags' list view.
        /// </summary>
        public bool UIShowTags { get; set; } = true;
        /// <summary>
        ///     Use horizontal layout for histories.
        /// </summary>
        public bool UIUseHorizontalLayout { get; set; }
        /// <summary>
        ///     Files' display mode in unstage view.
        /// </summary>
        public FilesDisplayMode UIUnstageDisplayMode { get; set; } = FilesDisplayMode.Grid;
        /// <summary>
        ///     Files' display mode in staged view.
        /// </summary>
        public FilesDisplayMode UIStagedDisplayMode { get; set; } = FilesDisplayMode.Grid;
        /// <summary>
        ///     Using datagrid instead of tree in changes.
        /// </summary>
        public bool UIUseListInChanges { get; set; }
        /// <summary>
        ///     Use one side diff instead of two sides.
        /// </summary>
        public bool UIUseOneSideDiff { get; set; }
        #endregion

        #region SETTING_REPOS
        /// <summary>
        ///     Groups for repositories.
        /// </summary>
        public List<Group> Groups { get; set; } = new List<Group>();
        /// <summary>
        ///     Watched repositories.
        /// </summary>
        public List<Repository> Repositories { get; set; } = new List<Git.Repository>();
        #endregion

        #region METHODS_LOAD_SAVE
        /// <summary>
        ///     Load preference from disk.
        /// </summary>
        /// <returns>Loaded preference instance.</returns>
        public static void Load() {
            if (!File.Exists(SAVE_PATH)) {
                instance = new Preference();
            } else {
                instance = JsonSerializer.Deserialize<Preference>(File.ReadAllText(SAVE_PATH));
            }
        }

        /// <summary>
        ///     Save current preference into disk.
        /// </summary>
        public static void Save() {
            if (instance == null) return;

            var dir = Path.GetDirectoryName(SAVE_PATH);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var data = JsonSerializer.Serialize(instance, new JsonSerializerOptions() { WriteIndented = true });
            File.WriteAllText(SAVE_PATH, data);
        }
        #endregion

        #region METHODS_ON_GROUP
        /// <summary>
        ///     Add new group(virtual folder).
        /// </summary>
        /// <param name="name">Display name.</param>
        /// <param name="parentId">Parent group ID.</param>
        /// <returns>Added group instance.</returns>
        public Group AddGroup(string name, string parentId) {
            var group = new Group() {
                Name = name,
                Id = Guid.NewGuid().ToString(),
                ParentId = parentId,
                IsExpended = false,
            };

            Groups.Add(group);
            Groups.Sort((l, r) => l.Name.CompareTo(r.Name));

            return group;
        }

        /// <summary>
        ///     Find group by ID.
        /// </summary>
        /// <param name="id">Unique ID</param>
        /// <returns>Founded group's instance.</returns>
        public Group FindGroup(string id) {
            foreach (var group in Groups) {
                if (group.Id == id) return group;
            }
            return null;
        }

        /// <summary>
        ///     Rename group.
        /// </summary>
        /// <param name="id">Unique ID</param>
        /// <param name="newName">New name.</param>
        public void RenameGroup(string id, string newName) {
            foreach (var group in Groups) {
                if (group.Id == id) {
                    group.Name = newName;
                    break;
                }
            }

            Groups.Sort((l, r) => l.Name.CompareTo(r.Name));
        }

        /// <summary>
        ///     Remove a group.
        /// </summary>
        /// <param name="id">Unique ID</param>
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
        #endregion

        #region METHODS_ON_REPOS
        /// <summary>
        ///     Add repository.
        /// </summary>
        /// <param name="path">Local storage path.</param>
        /// <param name="groupId">Group's ID</param>
        /// <returns>Added repository instance.</returns>
        public Repository AddRepository(string path, string groupId) {
            var repo = FindRepository(path);
            if (repo != null) return repo;

            var dir = new DirectoryInfo(path);
            repo = new Repository() {
                Path = dir.FullName,
                Name = dir.Name,
                GroupId = groupId,
                LastOpenTime = 0,
            };

            Repositories.Add(repo);
            Repositories.Sort((l, r) => l.Name.CompareTo(r.Name));
            return repo;
        }

        /// <summary>
        ///     Find repository by path.
        /// </summary>
        /// <param name="path">Local storage path.</param>
        /// <returns>Founded repository instance.</returns>
        public Repository FindRepository(string path) {
            var dir = new DirectoryInfo(path);
            foreach (var repo in Repositories) {
                if (repo.Path == dir.FullName) return repo;
            }
            return null;
        }

        /// <summary>
        ///     Change a repository's display name in RepositoryManager.
        /// </summary>
        /// <param name="path">Local storage path.</param>
        /// <param name="newName">New name</param>
        public void RenameRepository(string path, string newName) {
            var repo = FindRepository(path);
            if (repo == null) return;

            repo.Name = newName;
            Repositories.Sort((l, r) => l.Name.CompareTo(r.Name));
        }

        /// <summary>
        ///     Remove a repository in RepositoryManager.
        /// </summary>
        /// <param name="path">Local storage path.</param>
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
    }
}
