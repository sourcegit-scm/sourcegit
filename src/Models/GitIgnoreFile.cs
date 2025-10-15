using System.Collections.Generic;
using System.IO;
using Avalonia.Media;

namespace SourceGit.Models
{
    public class GitIgnoreFile
    {
        public static readonly List<GitIgnoreFile> Supported = [new(true), new(false)];

        public bool IsShared { get; set; }
        public string File => IsShared ? ".gitignore" : "<git_dir>/info/exclude";
        public string Desc => IsShared ? "Shared" : "Private";
        public IBrush Brush => IsShared ? Brushes.Green : Brushes.Gray;

        public GitIgnoreFile(bool isShared)
        {
            IsShared = isShared;
        }

        public string GetFullPath(string repoPath, string gitDir)
        {
            return IsShared ? Path.Combine(repoPath, ".gitignore") : Path.Combine(gitDir, "info", "exclude");
        }
    }
}
