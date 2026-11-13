using System.Collections.Generic;

namespace SourceGit.Models
{
    /// <summary>
    ///     A "project" is a user-defined container that owns one or more directories.
    ///     Repositories are discovered by scanning those directories and are NOT stored
    ///     here - they are rebuilt on every scan so that the file system stays the source
    ///     of truth.
    /// </summary>
    public class Project
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Directories { get; set; } = new List<string>();
    }

    /// <summary>
    ///     Root object persisted to projects.json.
    ///     Nodes are expanded by default, so only the paths the user collapsed are stored.
    /// </summary>
    public class ProjectsSaveModel
    {
        public List<Project> Projects { get; set; } = new List<Project>();
        public List<string> Collapsed { get; set; } = new List<string>();
    }
}
