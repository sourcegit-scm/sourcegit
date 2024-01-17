using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

namespace SourceGit.Models {
    /// <summary>
    ///     用于更新过滤器的参数
    /// </summary>
    public class FilterUpdateParam {
        /// <summary>
        ///     是否是添加过滤的操作，false代表删除
        /// </summary>
        public bool IsAdd = false;

        /// <summary>
        ///     过滤内容
        /// </summary>
        public string Name = "";
    }

    /// <summary>
    ///     仓库
    /// </summary>
    public class Repository {

        #region PROPERTIES_SAVED
        public string Name {
            get => name;
            set {
                if (name != value) {
                    name = value;
                    Watcher.NotifyDisplayNameChanged(this);
                }
            }
        }

        public string Path { get; set; } = "";
        public string GitDir { get; set; } = "";
        public long LastOpenTime { get; set; } = 0;
        public List<SubTree> SubTrees { get; set; } = new List<SubTree>();
        public List<string> Filters { get; set; } = new List<string>();
        public List<string> CommitMessages { get; set; } = new List<string>();

        public int Bookmark {
            get { return bookmark; }
            set {
                if (value != bookmark) {
                    bookmark = value;
                    Watcher.NotifyBookmarkChanged(this);
                }
            }
        }
        #endregion

        #region PROPERTIES_RUNTIME
        [JsonIgnore] public List<Remote> Remotes = new List<Remote>();
        [JsonIgnore] public List<Branch> Branches = new List<Branch>();
        [JsonIgnore] public GitFlow GitFlow = new GitFlow();
        #endregion

        /// <summary>
        ///     记录历史输入的提交信息
        /// </summary>
        /// <param name="message"></param>
        public void PushCommitMessage(string message) {
            if (string.IsNullOrEmpty(message)) return;

            int exists = CommitMessages.Count;
            if (exists > 0) {
                var last = CommitMessages[0];
                if (last == message) return;
            }

            if (exists >= 10) {
                CommitMessages.RemoveRange(9, exists - 9);
            }

            CommitMessages.Insert(0, message);
        }

        /// <summary>
        ///     判断一个文件是否在GitDir中
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public bool ExistsInGitDir(string file) {
            if (string.IsNullOrEmpty(file)) return false;
            string fullpath = System.IO.Path.Combine(GitDir, file);
            return Directory.Exists(fullpath) || File.Exists(fullpath);
        }
        
        /// <summary>
        ///     更新提交记录过滤器
        /// </summary>
        /// <param name="param">更新参数</param>
        /// <returns>是否发生了变化</returns>
        public bool UpdateFilters(FilterUpdateParam param = null) {
            lock (updateFilterLock) {
                bool changed = false;

                // 填写了参数就仅增删
                if (param != null) {
                    if (param.IsAdd) { 
                        if (!Filters.Contains(param.Name)) {
                            Filters.Add(param.Name);
                            changed = true;
                        }
                    } else {
                        if (Filters.Contains(param.Name)) {
                            Filters.Remove(param.Name);
                            changed = true;
                        }
                    }

                    return changed;
                }

                // 未填写参数就检测，去掉无效的过滤
                if (Filters.Count > 0) {
                    var invalidFilters = new List<string>();
                    var branches = new Commands.Branches(Path).Result();
                    var tags = new Commands.Tags(Path).Result();

                    foreach (var filter in Filters) {
                        if (filter.StartsWith("refs/")) {
                            if (branches.FindIndex(b => b.FullName == filter) < 0) invalidFilters.Add(filter);
                        } else {
                            if (tags.FindIndex(t => t.Name == filter) < 0) invalidFilters.Add(filter);
                        }
                    }

                    if (invalidFilters.Count > 0) {
                        foreach (var filter in invalidFilters) Filters.Remove(filter);
                        return true;
                    }
                }

                return false;
            }
        }

        private readonly object updateFilterLock = new object();
        private string name = string.Empty;
        private int bookmark = 0;
    }
}
