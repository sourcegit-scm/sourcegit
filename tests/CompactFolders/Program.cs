using System.Xml.Linq;
using SourceGit.Models;
using SourceGit.ViewModels;
using GitObject = SourceGit.Models.Object;

var passed = 0;
void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
void Test(string name, Action test)
{
    test();
    Console.WriteLine($"PASS {name}");
    passed++;
}
async Task TestAsync(string name, Func<Task> test)
{
    await test();
    Console.WriteLine($"PASS {name}");
    passed++;
}
Change File(string path) => new() { Path = path, Index = ChangeState.Modified };
List<ChangeTreeNode> Build(params Change[] changes) => ChangeTreeNode.Build(changes, [], true);
GitObject Obj(string path, ObjectType type = ObjectType.Tree) => new() { Path = path, Type = type, SHA = path + "-sha" };

// Structure checks complement the separately run Avalonia headless interaction tests.
Test("changes menu: compaction belongs only to the Tree submenu with visible checked state", () =>
{
    var root = new DirectoryInfo(AppContext.BaseDirectory);
    while (root != null && !System.IO.File.Exists(Path.Combine(root.FullName, "src", "Views", "ChangeViewModeSwitcher.axaml")))
        root = root.Parent;
    Check(root != null, "Could not locate ChangeViewModeSwitcher.axaml");
    var document = XDocument.Load(Path.Combine(root.FullName, "src", "Views", "ChangeViewModeSwitcher.axaml"));
    XNamespace av = "https://github.com/avaloniaui";
    var flyout = document.Descendants(av + "MenuFlyout").Single();
    var modes = flyout.Elements().ToList();
    Check(modes.Count == 3 && modes.All(x => x.Name == av + "MenuItem"), "Only the three modes belong at the top level");
    Check((string)modes[0].Attribute("Click") == "SwitchToList" && (string)modes[1].Attribute("Click") == "SwitchToGrid", "List/Grid selection lost");
    var tree = modes[2];
    Check(tree.Attribute("Click") == null && tree.Attribute("SubmenuOpened") == null, "Opening Tree or bubbling a toggle must not switch modes");
    var children = tree.Elements(av + "MenuItem").ToList();
    Check(children.Count == 1 && !tree.Elements(av + "Separator").Any(), "Tree contains only the requested compaction checkbox");
    var compact = children[0];
    Check((string)compact.Attribute("ToggleType") == "CheckBox" && compact.Attribute("Click") == null, "Compaction must remain an independent checkbox");
    var binding = (string)compact.Attribute("IsChecked");
    Check(binding.Contains("EnableCompactFoldersInChangesTree") && binding.Contains("Mode=TwoWay"), "Persisted checked-state binding lost");
    var check = compact.Descendants(av + "CheckBox").Single();
    Check(((string)check.Attribute("IsChecked")).Contains("EnableCompactFoldersInChangesTree"), "Custom menu template requires an explicit visible checkbox icon");
    Check((string)check.Attribute("IsHitTestVisible") == "False" && (string)check.Attribute("Focusable") == "False", "Icon must not intercept menu input");
    foreach (var mode in modes)
    {
        var icon = mode.Element(av + "MenuItem.Icon").Element(av + "Path");
        Check(icon != null && (string)icon.Attribute("Width") == "12" && (string)icon.Attribute("Height") == "12", "Preserve original mode icons");
        Check(mode.Attribute("ToggleType") == null && !mode.Descendants(av + "RadioButton").Any(), "No parent radio controls");
    }
});
Test("changes: deep chain keeps file and original path", () =>
{
    var file = File("main/java/com/example/service/File.cs");
    var tree = Build(file);
    Check(tree.Count == 1, "One root expected");
    var folder = tree[0];
    Check(folder.DisplayName == "main / java / com / example / service", folder.DisplayName);
    Check(folder.FullPath == "main/java/com/example/service", folder.FullPath);
    Check(folder.Depth == 0 && folder.Children[0].Depth == 1, "Display depth should shrink");
    Check(folder.Children[0].DisplayName == "File.cs", "File must remain its own row");
    Check(ReferenceEquals(folder.Children[0].Change, file), "File action target changed");
    Check(file.Path == "main/java/com/example/service/File.cs", "Underlying path changed");
});
Test("changes: branches and mixed siblings remain distinct", () =>
{
    var tree = Build(File("src/main/java/A.cs"), File("src/test/java/B.cs"), File("src/README.md"));
    var root = tree.Single();
    Check(root.DisplayName == "src" && root.Children.Count == 3, "Collapsed a branch");
    Check(root.Children[0].DisplayName == "main / java", "Main chain not compacted");
    Check(root.Children[1].DisplayName == "test / java", "Test chain not compacted");
    Check(root.Children[2].DisplayName == "README.md", "File sibling lost");
});
Test("changes: Unicode and spaces survive", () =>
{
    var file = File("资料/with spaces/人才详情/文件 name.cs");
    var root = Build(file).Single();
    Check(root.DisplayName == "资料 / with spaces / 人才详情", root.DisplayName);
    Check(root.Children.Single().FullPath == file.Path, "Unicode path changed");
    Check(root.Children.Single().DisplayName == "文件 name.cs", "Filename changed");
});
Test("changes: collapsed ancestor survives compaction", () =>
{
    var root = ChangeTreeNode.Build([File("a/b/c/file")], ["a"], true).Single();
    Check(!root.IsExpanded, "Folded ancestor was overwritten by expanded descendant");
});
Test("changes: collapsed descendant survives compaction", () =>
{
    var root = ChangeTreeNode.Build([File("a/b/c/file")], ["a/b/c"], true).Single();
    Check(!root.IsExpanded, "Folded deepest directory was lost");
});
Test("changes: disabling compact mode preserves normal hierarchy", () =>
{
    var root = ChangeTreeNode.Build([File("a/b/c/file")], [], false).Single();
    Check(root.DisplayName == "a" && root.Children.Single().DisplayName == "b", "Option ignored");
    Check(root.Children.Single().Children.Single().Depth == 2, "Wrong ordinary depth");
});
Test("changes: filtered rebuild retains original file object", () =>
{
    var file = File("a/b/c/File.cs");
    var all = Build(file, File("a/Other.cs"));
    var filtered = Build(file);
    Check(all[0].DisplayName == "a", "Branch should not compact");
    Check(filtered[0].DisplayName == "a / b / c", "Filtered chain should compact");
    Check(ReferenceEquals(filtered[0].Children[0].Change, file), "Selection identity changed");
});

await TestAsync("revision: lazy deep chain stops before file", async () =>
{
    var file = Obj("a/b/c/file name.txt", ObjectType.Blob);
    var calls = new List<string>();
    var data = new Dictionary<string, List<GitObject>>
    {
        ["a/"] = [Obj("a/b")],
        ["a/b/"] = [Obj("a/b/c")],
        ["a/b/c/"] = [file],
    };
    Task<List<GitObject>> Load(string path)
    {
        calls.Add(path);
        return Task.FromResult(data[path]);
    }
    var node = new RevisionFileTreeNode { Backend = Obj("a") };
    await node.PrepareAsync(Load, true);
    Check(node.Name == "a / b / c", node.Name);
    Check(node.Backend.Path == "a/b/c", "Folder operations need deepest actual path");
    Check(node.Children.Count == 1 && ReferenceEquals(node.Children[0].Backend, file), "File identity changed");
    Check(node.Children[0].Name == "file name.txt", "File merged into folder");
    Check(calls.SequenceEqual(new[] { "a/", "a/b/", "a/b/c/" }), "Unexpected lazy query sequence");
    await node.PrepareAsync(Load, true);
    Check(calls.Count == 3, "Prepared folders should not query again");
});
await TestAsync("revision: branching children stay lazy", async () =>
{
    var calls = new List<string>();
    var node = new RevisionFileTreeNode { Backend = Obj("src") };
    await node.PrepareAsync(path =>
    {
        calls.Add(path);
        return Task.FromResult(new List<GitObject> { Obj("src/main"), Obj("src/test") });
    }, true);
    Check(node.Name == "src" && node.Children.Count == 2, "Branch was merged");
    Check(calls.SequenceEqual(new[] { "src/" }), "Must not recursively load branching children");
    Check(node.Children.All(child => child.Children.Count == 0), "Branch children must remain lazy");
});
await TestAsync("revision: mixed directory and file stop compaction", async () =>
{
    var file = Obj("src/README.md", ObjectType.Blob);
    var node = new RevisionFileTreeNode { Backend = Obj("src") };
    await node.PrepareAsync(_ => Task.FromResult(new List<GitObject> { Obj("src/main"), file }), true);
    Check(node.Name == "src", "Mixed parent was merged");
    Check(node.Children.Any(child => ReferenceEquals(child.Backend, file)), "File backend lost");
});
await TestAsync("revision: Unicode, spaces, and immutable backends", async () =>
{
    var original = Obj("资料");
    var deepest = Obj("资料/with spaces");
    var file = Obj("资料/with spaces/人才.cs", ObjectType.Blob);
    var node = new RevisionFileTreeNode { Backend = original };
    await node.PrepareAsync(path => Task.FromResult(path == "资料/" ? new List<GitObject> { deepest } : new List<GitObject> { file }), true);
    Check(node.Name == "资料 / with spaces", node.Name);
    Check(original.Path == "资料" && deepest.Path == "资料/with spaces", "Original objects mutated");
    Check(ReferenceEquals(node.Backend, deepest), "Expected real terminal folder object");
    Check(node.Children.Single().Name == "人才.cs", "Unicode file name changed");
});
await TestAsync("revision: blobs and submodules never load or compact", async () =>
{
    foreach (var type in new[] { ObjectType.Blob, ObjectType.Commit })
    {
        var backend = Obj("a/b/file", type);
        var node = new RevisionFileTreeNode { Backend = backend };
        await node.PrepareAsync(_ => throw new InvalidOperationException("Leaf queried as directory"), true);
        Check(node.Name == "file" && ReferenceEquals(node.Backend, backend), "Leaf changed");
    }
});
await TestAsync("revision: empty folder is stable", async () =>
{
    var node = new RevisionFileTreeNode { Backend = Obj("empty") };
    var calls = 0;
    Task<List<GitObject>> Load(string _)
    {
        calls++;
        return Task.FromResult(new List<GitObject>());
    }
    await node.PrepareAsync(Load, true);
    await node.PrepareAsync(Load, true);
    Check(node.Name == "empty" && node.Children.Count == 0, "Empty folder changed");
    Check(calls == 1, "Empty child cache was ignored");
});
await TestAsync("revision: toggling compaction restores cached hierarchy", async () =>
{
    var a = Obj("a");
    var b = Obj("a/b");
    var file = Obj("a/b/file", ObjectType.Blob);
    var calls = 0;
    Task<List<GitObject>> Load(string path)
    {
        calls++;
        return Task.FromResult(path == "a/" ? new List<GitObject> { b } : new List<GitObject> { file });
    }
    var node = new RevisionFileTreeNode { Backend = a, IsExpanded = true };
    await node.PrepareAsync(Load, true);
    Check(node.Name == "a / b" && node.IsExpanded, "Initial compact state incorrect");
    node.IsExpanded = false;
    await node.PrepareAsync(Load, false);
    Check(node.Name == "a" && ReferenceEquals(node.Backend, a), "Original parent was not restored");
    Check(ReferenceEquals(node.Children.Single().Backend, b), "Original child was not restored");
    Check(!node.IsExpanded && !node.Children.Single().IsExpanded, "Collapse did not survive mode toggle");
    await node.PrepareAsync(Load, true);
    Check(node.Name == "a / b" && calls == 2, "Toggling reloaded cached directories");
    Check(ReferenceEquals(node.Children.Single().Backend, file), "File action object changed after toggle");
});
await TestAsync("revision: concurrent child loads share one query", async () =>
{
    var pending = new TaskCompletionSource<List<GitObject>>();
    var calls = 0;
    var node = new RevisionFileTreeNode { Backend = Obj("a") };
    Task<List<GitObject>> Load(string path)
    {
        calls++;
        return pending.Task;
    }
    var first = node.LoadChildrenAsync(Load);
    var second = node.LoadChildrenAsync(Load);
    pending.SetResult([Obj("a/file", ObjectType.Blob)]);
    await Task.WhenAll(first, second);
    Check(calls == 1 && node.Children.Count == 1, "Concurrent expansion duplicated query or children");
});
await TestAsync("revision: explicit collapsed descendant survives enabling compaction", async () =>
{
    var node = new RevisionFileTreeNode { Backend = Obj("a"), IsExpanded = true };
    Task<List<GitObject>> Load(string path) => Task.FromResult(path == "a/" ? new List<GitObject> { Obj("a/b") } : new List<GitObject> { Obj("a/b/file", ObjectType.Blob) });
    await node.LoadChildrenAsync(Load);
    node.Children.Single().IsExpanded = false;
    await node.PrepareAsync(Load, true);
    Check(node.Name == "a / b" && !node.IsExpanded, "Explicit collapsed child was expanded by toggle");
    await node.PrepareAsync(Load, false);
    Check(!node.Children.Single().IsExpanded, "Collapsed descendant state was lost");
});
await TestAsync("revision: expanded deep parent stays expanded over unloaded descendants", async () =>
{
    var node = new RevisionFileTreeNode { Backend = Obj("a"), IsExpanded = true };
    var data = new Dictionary<string, List<GitObject>> { ["a/"] = [Obj("a/b")], ["a/b/"] = [Obj("a/b/c")], ["a/b/c/"] = [Obj("a/b/c/file", ObjectType.Blob)] };
    await node.PrepareAsync(path => Task.FromResult(data[path]), true);
    Check(node.Name == "a / b / c" && node.IsExpanded, "Implicit default collapsed state became explicit during recursive preparation");
});
await TestAsync("revision: repeated preparation never exposes a transient ancestor path", async () =>
{
    var a = Obj("a");
    var b = Obj("a/b");
    var file = Obj("a/b/file", ObjectType.Blob);
    var node = new RevisionFileTreeNode { Backend = a };
    Task<List<GitObject>> Load(string path) => Task.FromResult(path == "a/" ? new List<GitObject> { b } : new List<GitObject> { file });
    await node.LoadChildrenAsync(Load);
    var child = node.Children.Single();
    await node.PrepareAsync(Load, true);
    var observed = 0;
    child.PropertyChangedForTest += _ =>
    {
        observed++;
        Check(ReferenceEquals(node.Backend, b) && node.Backend.Path == "a/b", "Re-prepare temporarily exposed ancestor operation path");
        Check(node.Name == "a / b", "Re-prepare temporarily exposed incomplete label");
        Check(ReferenceEquals(node.Children.Single().Backend, file), "Re-prepare temporarily exposed intermediate folder as file row");
    };
    await node.PrepareAsync(Load, true);
    Check(observed > 0, "Expected recursive preparation notifications");
});
Console.WriteLine($"{passed} compact-folder tests passed.");
