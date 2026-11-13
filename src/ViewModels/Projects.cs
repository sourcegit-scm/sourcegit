using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
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

        public bool IsScanning
        {
            get => _isScanning;
            private set => SetProperty(ref _isScanning, value);
        }

        public ProjectTreeNode CreateProject(IEnumerable<string> directories)
        {
            var dirs = directories.Select(Models.RepositoryScanner.Normalize).Where(d => !string.IsNullOrEmpty(d)).ToList();
            if (dirs.Count == 0)
                return null;

            // Reuse a project that covers exactly the same directories: pressing "+ 工程" twice
            // on the same folder used to produce "docs", "docs 2", "docs 3".
            var existing = Roots.FirstOrDefault(r => r.Project != null &&
                r.Project.Directories.Count == dirs.Count &&
                r.Project.Directories.All(d => dirs.Contains(d, StringComparer.OrdinalIgnoreCase)));
            if (existing != null)
            {
                Models.ScanLog.Write($"projects: reuse existing project '{existing.Name}'");
                return existing;
            }

            var baseName = Path.GetFileName(dirs[0]);
            if (string.IsNullOrEmpty(baseName))
                baseName = dirs[0];

            var project = new Models.Project { Name = MakeUniqueName(baseName, null) };
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
            node.PropertyChanged += OnNodePropertyChanged;

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

        public void RenameProject(ProjectTreeNode projectNode, string newName)
        {
            if (projectNode?.Project == null)
                return;

            var name = (newName ?? string.Empty).Trim();
            if (name.Length == 0 || string.Equals(name, projectNode.Project.Name, StringComparison.Ordinal))
                return;

            projectNode.Project.Name = MakeUniqueName(name, projectNode);
            projectNode.Name = projectNode.Project.Name;
            Save();
        }

        public void RemoveProject(ProjectTreeNode projectNode)
        {
            if (projectNode?.Project == null)
                return;

            projectNode.PropertyChanged -= OnNodePropertyChanged;
            _projects.Remove(projectNode.Project);
            Roots.Remove(projectNode);
            Save();
        }

        /// <summary>
        ///     Removes one configured directory from a project. <paramref name="rootNode"/>
        ///     must be the tree node created for that directory (a direct child of the
        ///     project node), not an intermediate directory node.
        /// </summary>
        public void RemoveDirectory(ProjectTreeNode projectNode, ProjectTreeNode rootNode)
        {
            if (projectNode?.Project == null || rootNode?.Parent != projectNode)
                return;

            var index = projectNode.Project.Directories.FindIndex(d => string.Equals(Models.RepositoryScanner.Key(d), Models.RepositoryScanner.Key(rootNode.FullPath), StringComparison.Ordinal));
            if (index < 0)
                return;

            projectNode.Project.Directories.RemoveAt(index);
            projectNode.Children.Remove(rootNode);
            Save();
            UpdateAggregates();
        }

        public void CancelScan()
        {
            _scanCts?.Cancel();
        }

        /// <summary>
        ///     Removes one discovered node (a repository, or a directory below the configured
        ///     ones) from the current tree only. Nothing is persisted, so the next scan brings
        ///     it back. That is intentional: a permanent exclusion list would make a mistake
        ///     hard to undo, and adding the same path back is a normal thing to want.
        ///     Directories left empty by the removal are dropped as well, so the tree does not
        ///     keep a dead branch around.
        /// </summary>
        public void RemoveNode(ProjectTreeNode node)
        {
            if (node == null || node.Parent == null || node.IsProject)
                return;

            node.PropertyChanged -= OnNodePropertyChanged;
            node.Parent.Children.Remove(node);

            var parent = node.Parent;
            while (parent != null && !parent.IsProject && parent.Children.Count == 0)
            {
                parent.PropertyChanged -= OnNodePropertyChanged;

                var grandParent = parent.Parent;
                if (grandParent == null)
                    break;

                grandParent.Children.Remove(parent);
                parent = grandParent;
            }

            UpdateAggregates();
        }

        /// <summary>
        ///     Finds the repository node that represents the given absolute path.
        ///     Paths are compared with the same normalisation the scanner uses, so a
        ///     tab opened from a path with a different slash style or casing still matches.
        ///     Returns null when the repository does not belong to any project.
        /// </summary>
        public ProjectTreeNode FindRepository(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            var key = Models.RepositoryScanner.Key(path);
            foreach (var root in Roots)
            {
                var found = FindRepository(root, key);
                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>
        ///     Expands every ancestor of the given node so that it becomes visible.
        ///     The expansion is deliberately kept out of the persisted collapsed set:
        ///     it follows the active tab, it is not a preference of the user.
        /// </summary>
        public void RevealRepository(ProjectTreeNode repositoryNode)
        {
            if (repositoryNode == null)
                return;

            _suppressCollapseSave = true;
            try
            {
                var parent = repositoryNode.Parent;
                while (parent != null)
                {
                    parent.IsExpanded = true;
                    parent = parent.Parent;
                }
            }
            finally
            {
                _suppressCollapseSave = false;
            }
        }

        /// <summary>
        ///     Recomputes the status shown on the tree: repositories that are open in a tab
        ///     contribute the state the repository page already tracks (which updates on every
        ///     git operation), the rest are queried in the background with a working-copy-only
        ///     `git status`. Runs are serialised: a second request cancels the previous one.
        /// </summary>
        public async Task RefreshStatusAsync()
        {
            HookLauncher();

            // Cancel only: disposing the source while a query may still hold its token would
            // throw on the cancelled path.
            _statusCts?.Cancel();

            var cts = new CancellationTokenSource();
            _statusCts = cts;

            var repositories = new List<ProjectTreeNode>();
            foreach (var root in Roots)
                CollectRepositories(root, repositories);

            if (repositories.Count == 0)
                return;

            Dictionary<string, int> dirty;
            try
            {
                dirty = await Models.RepositoryStatusScanner.QueryDirtyAsync(repositories.Select(n => n.FullPath).ToList(), cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            finally
            {
                if (ReferenceEquals(_statusCts, cts))
                    _statusCts = null;
            }

            if (cts.IsCancellationRequested)
                return;

            foreach (var node in repositories)
            {
                var count = dirty.TryGetValue(Models.RepositoryScanner.Key(node.FullPath), out var changes) ? changes : 0;
                node.LocalChanges = count;
                node.Status = count > 0 ? Models.DirtyState.HasLocalChanges : Models.DirtyState.None;
            }

            // Open pages win over the scan: they know about pending pushes and pulls too.
            MirrorOpenPages();
            UpdateAggregates();
        }

        /// <summary>
        ///     Mirrors the state of a repository page onto its tree node. Called whenever a page
        ///     reports a new dirty state, so a commit/stage/pull in a tab shows up in the tree
        ///     immediately instead of waiting for the next scan.
        /// </summary>
        public void MirrorPageStatus(LauncherPage page)
        {
            if (page?.Node == null)
                return;

            var node = FindRepository(page.Node.Id);
            if (node == null)
                return;

            if (page.Data is Repository repository)
                node.LocalChanges = repository.LocalChangesCount;

            node.Status = page.DirtyState;
            UpdateAggregates();
        }

        /// <summary>
        ///     Subscribes to the launcher's pages once, so status changes made in a tab reach the
        ///     tree. Safe to call repeatedly: it is a no-op once the current launcher is hooked.
        /// </summary>
        public void HookLauncher()
        {
            var launcher = App.GetLauncher();
            if (launcher == null || ReferenceEquals(_hookedLauncher, launcher))
                return;

            if (_hookedLauncher != null)
            {
                _hookedLauncher.Pages.CollectionChanged -= OnPagesChanged;
                foreach (var page in _hookedLauncher.Pages)
                    page.PropertyChanged -= OnPagePropertyChanged;
            }

            _hookedLauncher = launcher;
            _hookedLauncher.Pages.CollectionChanged += OnPagesChanged;
            foreach (var page in _hookedLauncher.Pages)
                page.PropertyChanged += OnPagePropertyChanged;
        }

        private void OnPagesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                    if (item is LauncherPage removed)
                        removed.PropertyChanged -= OnPagePropertyChanged;
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                    if (item is LauncherPage added)
                        added.PropertyChanged += OnPagePropertyChanged;
            }

            // A closed tab no longer updates its node, so re-query everything below.
            _ = RefreshStatusAsync();
        }

        private void OnPagePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LauncherPage.DirtyState) && sender is LauncherPage page)
                MirrorPageStatus(page);
        }

        private void MirrorOpenPages()
        {
            var launcher = _hookedLauncher;
            if (launcher == null)
                return;

            foreach (var page in launcher.Pages)
                MirrorPageStatus(page);
        }

        private static void CollectRepositories(ProjectTreeNode node, List<ProjectTreeNode> result)
        {
            if (node.IsRepository)
            {
                result.Add(node);
                return;
            }

            foreach (var child in node.Children)
                CollectRepositories(child, result);
        }

        private void UpdateAggregates()
        {
            foreach (var root in Roots)
                UpdateAggregates(root);
        }

        private int UpdateAggregates(ProjectTreeNode node)
        {
            if (node.IsRepository)
            {
                node.DirtyRepositories = node.IsRepositoryDirty ? 1 : 0;
                return node.DirtyRepositories;
            }

            var count = 0;
            foreach (var child in node.Children)
                count += UpdateAggregates(child);

            node.DirtyRepositories = count;
            return count;
        }

        private static ProjectTreeNode FindRepository(ProjectTreeNode node, string key)
        {
            if (node.IsRepository && string.Equals(Models.RepositoryScanner.Key(node.FullPath), key, StringComparison.Ordinal))
                return node;

            foreach (var child in node.Children)
            {
                var found = FindRepository(child, key);
                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>
        ///     Rebuilds one project's subtree. Scanning runs on the thread pool while the tree
        ///     is updated as repositories are found, so this must be called from the UI thread.
        ///     A running scan of the previous request is cancelled.
        /// </summary>
        public async Task RebuildAsync(ProjectTreeNode projectNode)
        {
            if (projectNode?.Project == null)
                return;

            var previous = _scanCts;
            var cts = new CancellationTokenSource();
            _scanCts = cts;
            previous?.Cancel();
            previous?.Dispose();

            var token = cts.Token;
            var dirs = projectNode.Project.Directories.ToArray();
            var entries = new List<ScanEntry>();
            IsScanning = true;
            Models.ScanLog.Write($"projects: rebuild '{projectNode.Name}' dirs={dirs.Length}");

            try
            {
                projectNode.Children.Clear();

                for (var i = 0; i < dirs.Length; i++)
                {
                    if (token.IsCancellationRequested)
                        return;

                    var index = i + 1;
                    var total = dirs.Length;
                    var dir = dirs[i];
                    StatusText = $"{projectNode.Name}: 扫描 ({index}/{total}) {dir}";

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

                    // The directory node is added before scanning starts, so the user sees the
                    // tree structure immediately and repositories appear under it as they are
                    // confirmed instead of all at once at the end.
                    var entry = new ScanEntry(dir, new ProjectTreeNode
                    {
                        // The first level under a project is the configured directory itself,
                        // named by its full path: using only the last segment made it collide
                        // with the project name ("docs → docs").
                        Name = Models.RepositoryScanner.Normalize(dir),
                        FullPath = dir,
                        Parent = projectNode,
                        IsExpanded = !_collapsed.Contains(Models.RepositoryScanner.Key(dir)),
                    });
                    entry.Node.PropertyChanged += OnNodePropertyChanged;
                    projectNode.Children.Add(entry.Node);
                    entries.Add(entry);

                    var found = new Progress<string>(repo =>
                    {
                        if (!entry.Reconciled && Roots.Contains(projectNode))
                            InsertRepository(entry.Node, dir, repo);
                    });

                    var progress = new Progress<string>(current =>
                        StatusText = $"{projectNode.Name}: 扫描 ({index}/{total}) {Shorten(current)}");

                    entry.Repositories = await Task.Run(() => Models.RepositoryScanner.ScanAsync(dir, progress, found, token), token);
                    Models.ScanLog.Write($"projects: dir '{dir}' returned {entry.Repositories.Count} repositories");
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                Native.OS.LogException(e);
                StatusText = $"扫描失败: {e.Message}";
                return;
            }
            finally
            {
                if (ReferenceEquals(_scanCts, cts))
                {
                    _scanCts = null;
                    IsScanning = false;
                }
            }

            // Drop the result when the project was removed while the scan was running.
            if (token.IsCancellationRequested || !Roots.Contains(projectNode))
                return;

            FinalizeTree(projectNode, entries, dirs.Length);

            // Repositories known now: fetch their working copy state once, in the background.
            await RefreshStatusAsync();
        }

        /// <summary>
        ///     Rebuilds every project once, so that the tree appears without the user
        ///     having to press 扫描 on every project after a restart.
        /// </summary>
        public void StartRestore()
        {
            if (_restored)
                return;

            _restored = true;
            _ = RestoreAllAsync();
        }

        private async Task RestoreAllAsync()
        {
            // Sequentially, not concurrently: RebuildAsync cancels the previous scan of the
            // same instance, so firing them all at once would cancel every one but the last.
            foreach (var root in Roots.ToList())
            {
                await RebuildAsync(root);
                await Task.Yield();
            }
        }

        private Projects()
        {
            Load();
        }

        /// <summary>
        ///     Makes sure the incremental insertions are complete (progress callbacks can
        ///     still be queued when the scan task returns), applies the "directory itself is
        ///     a repository" case, sorts children and refreshes the status line.
        /// </summary>
        private void FinalizeTree(ProjectTreeNode projectNode, List<ScanEntry> entries, int totalDirs)
        {
            _suppressCollapseSave = true;
            try
            {
                var total = 0;
                foreach (var entry in entries)
                {
                    entry.Reconciled = true;
                    total += entry.Repositories.Count;

                    foreach (var repo in entry.Repositories)
                        InsertRepository(entry.Node, entry.Dir, repo);

                    if (entry.Repositories.Count == 1 &&
                        string.Equals(entry.Repositories[0], Models.RepositoryScanner.Normalize(entry.Dir), StringComparison.OrdinalIgnoreCase))
                    {
                        entry.Node.IsRepository = true;
                    }

                    SortChildren(entry.Node);
                }

                StatusText = $"{projectNode.Name}: {entries.Count}/{totalDirs} 个目录 / {total} 个仓库";
                Models.ScanLog.Write($"projects: finalize '{projectNode.Name}' dirs={entries.Count}/{totalDirs} repos={total} topChildren={projectNode.Children.Count}");
            }
            finally
            {
                _suppressCollapseSave = false;
            }
        }

        private static string Shorten(string path)
        {
            var normalized = Models.RepositoryScanner.Normalize(path);
            var index = normalized.LastIndexOf('/');
            return index < 0 || index + 1 >= normalized.Length ? normalized : normalized.Substring(index + 1);
        }

        private string MakeUniqueName(string baseName, ProjectTreeNode except)
        {
            var name = baseName;
            var i = 2;
            while (_projects.Any(p => !ReferenceEquals(p, except?.Project) && string.Equals(p.Name, name, StringComparison.CurrentCultureIgnoreCase)))
                name = $"{baseName} {i++}";
            return name;
        }

        private void InsertRepository(ProjectTreeNode rootNode, string rootDir, string repoPath)
        {
            Models.ScanLog.Write($"projects: insert '{repoPath}' under '{rootDir}'");

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
                var full = Models.RepositoryScanner.Normalize(Path.Combine(Path.Combine(new[] { rootDir }.Concat(segments.Take(i + 1)).ToArray())));

                var child = cursor.Children.FirstOrDefault(c => string.Equals(c.Name, segments[i], StringComparison.CurrentCultureIgnoreCase));
                if (child == null)
                {
                    child = new ProjectTreeNode
                    {
                        Name = segments[i],
                        FullPath = full,
                        Parent = cursor,
                        IsRepository = isLeaf,
                        IsExpanded = !_collapsed.Contains(Models.RepositoryScanner.Key(full)),
                    };
                    child.PropertyChanged += OnNodePropertyChanged;
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

        private void OnNodePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ProjectTreeNode.IsExpanded) || _suppressCollapseSave)
                return;

            if (sender is not ProjectTreeNode node || string.IsNullOrEmpty(node.FullPath))
                return;

            var key = Models.RepositoryScanner.Key(node.FullPath);
            if (node.IsExpanded)
                _collapsed.Remove(key);
            else
                _collapsed.Add(key);

            Save();
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

                foreach (var key in model.Collapsed ?? new List<string>())
                {
                    if (!string.IsNullOrEmpty(key))
                        _collapsed.Add(key);
                }

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
                    node.PropertyChanged += OnNodePropertyChanged;
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
                var model = new Models.ProjectsSaveModel
                {
                    Projects = _projects,
                    Collapsed = _collapsed.ToList(),
                };

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

        private sealed class ScanEntry
        {
            public ScanEntry(string dir, ProjectTreeNode node)
            {
                Dir = dir;
                Node = node;
                Repositories = new List<string>();
            }

            public string Dir { get; }
            public ProjectTreeNode Node { get; }
            public List<string> Repositories { get; set; }
            public bool Reconciled { get; set; }
        }

        private readonly List<Models.Project> _projects = new List<Models.Project>();
        private readonly HashSet<string> _collapsed = new HashSet<string>();
        private CancellationTokenSource _scanCts = null;
        private CancellationTokenSource _statusCts = null;
        private Launcher _hookedLauncher = null;
        private bool _suppressCollapseSave = false;
        private bool _restored = false;
        private string _statusText = string.Empty;
        private bool _isScanning = false;
    }
}
