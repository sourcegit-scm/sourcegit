using System;
using System.Collections.Generic;
using System.IO;

namespace SourceGit {

    /// <summary>
    ///     User's preference settings. Serialized to 
    /// </summary>
    public class Preference {

        /// <summary>
        ///     Tools setting.
        /// </summary>
        public class ToolSetting {
            /// <summary>
            ///     Git executable file path.
            /// </summary>
            public string GitExecutable { get; set; }
            /// <summary>
            ///     Default clone directory.
            /// </summary>
            public string GitDefaultCloneDir { get; set; }
            /// <summary>
            ///     Selected merge tool.
            /// </summary>
            public int MergeTool { get; set; } = 0;
            /// <summary>
            ///     Executable file path for merge tool.
            /// </summary>
            public string MergeExecutable { get; set; } = "--";
        }

        /// <summary>
        ///     File's display mode.
        /// </summary>
        public enum FilesDisplayMode {
            Tree,
            List,
            Grid,
        }

        /// <summary>
        ///     Settings for UI.
        /// </summary>
        public class UISetting {
            /// <summary>
            ///     Use light theme?
            /// </summary>
            public bool UseLightTheme { get; set; }
            /// <summary>
            ///     Locale
            /// </summary>
            public string Locale { get; set; } = "en_US";
            /// <summary>
            ///     Main window width
            /// </summary>
            public double WindowWidth { get; set; }
            /// <summary>
            ///     Main window height
            /// </summary>
            public double WindowHeight { get; set; }
            /// <summary>
            ///     Move commit viewer from bottom to right
            /// </summary>
            public bool MoveCommitViewerRight { get; set; }
            /// <summary>
            ///     File's display mode in unstaged view.
            /// </summary>
            public FilesDisplayMode UnstageFileDisplayMode { get; set; }
            /// <summary>
            ///     File's display mode in staged view.
            /// </summary>
            public FilesDisplayMode StagedFileDisplayMode { get; set; }
            /// <summary>
            ///     Use DataGrid instead of TreeView in changes view.
            /// </summary>
            public bool UseListInChanges { get; set; }
            /// <summary>
            ///     Use combined instead of side-by-side mode in diff viewer.
            /// </summary>
            public bool UseCombinedDiff { get; set; }
        }

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

        #region SAVED_DATAS
        /// <summary>
        ///     Check for updates.
        /// </summary>
        public bool CheckUpdate { get; set; } = true;
        /// <summary>
        ///     Last UNIX timestamp to check for update.
        /// </summary>
        public int LastCheckUpdate { get; set; } = 0;
        /// <summary>
        ///     Settings for executables.
        /// </summary>
        public ToolSetting Tools { get; set; } = new ToolSetting();
        /// <summary>
        ///     Use light color theme.
        /// </summary>
        public UISetting UI { get; set; } = new UISetting();
        #endregion

        #region SETTING_REPOS
        /// <summary>
        ///     Groups for repositories.
        /// </summary>
        public List<Group> Groups { get; set; } = new List<Group>();
        /// <summary>
        ///     Watched repositories.
        /// </summary>
        public List<Git.Repository> Repositories { get; set; } = new List<Git.Repository>();
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

        /// <summary>
        ///     Check if given group has relations.
        /// </summary>
        /// <param name="parentId"></param>
        /// <param name="subId"></param>
        /// <returns></returns>
        public bool IsSubGroup(string parentId, string subId) {
            if (string.IsNullOrEmpty(parentId)) return false;
            if (parentId == subId) return true;

            var g = FindGroup(subId);
            if (g == null) return false;

            g = FindGroup(g.ParentId);
            while (g != null) {
                if (g.Id == parentId) return true;
                g = FindGroup(g.ParentId);
            }

            return false;
        }
        #endregion

        #region METHODS_ON_REPOS
        /// <summary>
        ///     Add repository.
        /// </summary>
        /// <param name="path">Local storage path.</param>
        /// <param name="groupId">Group's ID</param>
        /// <returns>Added repository instance.</returns>
        public Git.Repository AddRepository(string path, string groupId) {
            var repo = FindRepository(path);
            if (repo != null) return repo;

            var dir = new DirectoryInfo(path);
            repo = new Git.Repository() {
                Path = dir.FullName,
                Name = dir.Name,
                GroupId = groupId,
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
        public Git.Repository FindRepository(string path) {
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
