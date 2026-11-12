using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Avalonia.Collections;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    /// <summary>
    ///     Owns the user's projects and the left-side tree built from them.
    ///     Persisted to its own projects.json so that the big Preferences class (and
    ///     therefore the most conflict-prone file with upstream) is not touched.
    /// </summary>
    public class Projects : ObservableObject
    {
        public static Projects Instance
        {
            get;
        } = new Projects();

        public AvaloniaList<ProjectTreeNode> Roots
        {
            get;
        } = new AvaloniaList<ProjectTreeNode>();

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public ProjectTreeNode CreateProject(IEnumerable<string> directories)
        {
            var dirs = directories.Select(Models.RepositoryScanner.Normalize).Where(d => !string.IsNullOrEmpty(d)).ToList();
            if (dirs.Count == 0)
                return null;

            var baseName = Path.GetFileName(dirs[0]);
            if (string.IsNullOrEmpty(baseName))
                baseName = dirs[0];

            var project = new Models.Project { Name = MakeUniqueName(baseName) };
            project.Directories.AddRange(dirs);
            _projects.Add(project);

            var node = new ProjectTreeNode
            {
                Name = project.Name,
                FullPath = string.Empty,
                IsProject = true,
                IsExpanded = true,
                Project = project,
            };

            Roots.Add(node);
            Save();
            return node;
        }

        public bool AddDirectories(ProjectTreeNode projectNode, IEnumerable<string> directories)
        {
            if (projectNode?.Project == null)
                return false;

            var added = false;
            foreach (var dir in directories.Select(Models.RepositoryScanner.Normalize))
            {
                if (string.IsNullOrEmpty(dir))
                    continue;

                var existing = projectNode.Project.Directories.FirstOrDefault(d => string.Equals(d, dir, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                    continue;

                projectNode.Project.Directories.Add(dir);
                added = true;
            }

            if (added)
                Save();

            return added;
        }

        public void RemoveProject(ProjectTreeNode projectNode)
        {
            if (projectNode?.Project == null)
                return;

            _projects.Remove(projectNode.Project);
            Roots.Remove(projectNode);
            Save();
        }

        public async Task RebuildAsync(ProjectTreeNode projectNode)
        {
            if (projectNode?.Project == null)
                return;

            projectNode.Children.Clear();

            var total = 0;
            foreach (var dir in projectNode.Project.Directories.ToArray())
            {
                if (!Directory.Exists(dir))
                {
                    var missing = new ProjectTreeNode
                    {
                        Name = $"{dir} (不存在)",
                        FullPath = dir,
                        Parent = projectNode,
                    };
                    projectNode.Children.Add(missing);
                    continue;
                }

                StatusText = $"{projectNode.Name}: 正在扫描 {dir} ...";
                var repos = await Models.RepositoryScanner.ScanAsync(dir);
                total += repos.Count;

                var rootNode = new ProjectTreeNode
                {
                    Name = DisplayNameOf(dir),
                    FullPath = dir,
                    Parent = projectNode,
                    IsExpanded = true,
                };

                if (repos.Count == 1 && string.Equals(repos[0], dir, StringComparison.OrdinalIgnoreCase))
                {
                    rootNode.IsRepository = true;
                }
                else
                {
                    foreach (var repo in repos)
                        InsertRepository(rootNode, dir, repo);
                }

                projectNode.Children.Add(rootNode);
                SortChildren(rootNode);
            }

            StatusText = $"{projectNode.Name}: {projectNode.Project.Directories.Count} 个目录 / {total} 个仓库";
        }

        public void ForgetAllCachedTree()
        {
            Roots.Clear();
        }

        private Projects()
        {
            Load();
        }

        private static string DisplayNameOf(string dir)
        {
            var name = Path.GetFileName(dir);
            return string.IsNullOrEmpty(name) ? dir : name;
        }

        private string MakeUniqueName(string baseName)
        {
            var name = baseName;
            var i = 2;
            while (_projects.Any(p => string.Equals(p.Name, name, StringComparison.CurrentCultureIgnoreCase)))
                name = $"{baseName} {i++}";
            return name;
        }

        private static void InsertRepository(ProjectTreeNode rootNode, string rootDir, string repoPath)
        {
            var normalizedRoot = Models.RepositoryScanner.Normalize(rootDir);
            if (string.Equals(repoPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                rootNode.IsRepository = true;
                return;
            }

            var relative = Path.GetRelativePath(rootDir, repoPath.Replace('/', Path.DirectorySeparatorChar));
            var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (segments.Length == 0)
                return;

            var cursor = rootNode;
            for (var i = 0; i < segments.Length; i++)
            {
                if (string.IsNullOrEmpty(segments[i]))
                    continue;

                var isLeaf = i == segments.Length - 1;
                var full = Models.RepositoryScanner.Normalize(Path.Combine(rootDir, Path.Combine(segments.Take(i + 1).ToArray())));

                var child = cursor.Children.FirstOrDefault(c => string.Equals(c.Name, segments[i], StringComparison.CurrentCultureIgnoreCase));
                if (child == null)
                {
                    child = new ProjectTreeNode
                    {
                        Name = segments[i],
                        FullPath = full,
                        Parent = cursor,
                        IsRepository = isLeaf,
                        IsExpanded = true,
                    };
                    cursor.Children.Add(child);
                }
                else if (isLeaf)
                {
                    child.IsRepository = true;
                }

                cursor = child;
            }
        }

        private static void SortChildren(ProjectTreeNode node)
        {
            var sorted = node.Children.OrderBy(c => c.IsRepository ? 1 : 0)
                .ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            node.Children.Clear();
            foreach (var child in sorted)
                node.Children.Add(child);

            foreach (var child in node.Children)
                SortChildren(child);
        }

        private string StorePath
        {
            get => Path.Combine(Native.OS.BasicDirectories.ConfigDir, "projects.json");
        }

        private void Load()
        {
            try
            {
                var path = StorePath;
                if (!File.Exists(path))
                    return;

                var model = JsonSerializer.Deserialize(File.ReadAllText(path), JsonCodeGen.Default.ProjectsSaveModel);
                if (model?.Projects == null)
                    return;

                foreach (var project in model.Projects)
                {
                    if (string.IsNullOrEmpty(project.Name))
                        continue;

                    _projects.Add(project);

                    var node = new ProjectTreeNode
                    {
                        Name = project.Name,
                        FullPath = string.Empty,
                        IsProject = true,
                        IsExpanded = true,
                        Project = project,
                    };
                    Roots.Add(node);
                }
            }
            catch (Exception e)
            {
                Native.OS.LogException(e);
            }
        }

        public void Save()
        {
            try
            {
                var model = new Models.ProjectsSaveModel { Projects = _projects };
                var text = JsonSerializer.Serialize(model, JsonCodeGen.Default.ProjectsSaveModel);

                var finalFile = StorePath;
                var tmpFile = Path.Combine(Native.OS.BasicDirectories.ConfigDir, "projects_tmp.json");
                File.WriteAllText(tmpFile, text);

                if (File.Exists(finalFile))
                    File.Delete(finalFile);

                File.Move(tmpFile, finalFile);
            }
            catch (Exception e)
            {
                Native.OS.LogException(e);
            }
        }

        private readonly List<Models.Project> _projects = new List<Models.Project>();
        private string _statusText = string.Empty;
    }
}
