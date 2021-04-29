using System.Collections.Generic;
using System.Windows;

namespace SourceGit.Models {
    /// <summary>
    ///     提交记录
    /// </summary>
    public class Commit {
        public string SHA { get; set; } = "";
        public string ShortSHA => SHA.Substring(0, 8);
        public User Author { get; set; } = new User();
        public User Committer { get; set; } = new User();
        public string Subject { get; set; } = "";
        public string Message { get; set; } = "";
        public List<string> Parents { get; set; } = new List<string>();
        public List<Decorator> Decorators { get; set; } = new List<Decorator>();
        public bool HasDecorators => Decorators.Count > 0;
        public bool IsMerged { get; set; } = false;
        public Thickness Margin { get; set; } = new Thickness(0);
    }
}
