using System.Collections.Generic;

#if NET48
using Newtonsoft.Json;
#else
using System.Text.Json.Serialization;
#endif

namespace SourceGit.Models {

    /// <summary>
    ///     仓库
    /// </summary>
    public class Repository {

        #region PROPERTIES_SAVED
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public string GitDir { get; set; } = "";
        public string GroupId { get; set; } = "";
        public int Bookmark { get; set; } = 0;
        public List<SubTree> SubTrees { get; set; } = new List<SubTree>();
        public List<string> Filters { get; set; } = new List<string>();
        public List<string> CommitMessages { get; set; } = new List<string>();
        #endregion

        #region PROPERTIES_RUNTIME
        [JsonIgnore] public List<Remote> Remotes = new List<Remote>();
        [JsonIgnore] public List<Branch> Branches = new List<Branch>();
        [JsonIgnore] public GitFlow GitFlow = new GitFlow();
        #endregion

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
    }
}
