using System.Diagnostics;
using System.Xml.Linq;
using SourceGit.Commands;
using SourceGit.Models;

var tests = new (string Name, Func<Task> Run)[]
{
    ("root commit and immutable endpoints", Root),
    ("contiguous modifications", Contiguous),
    ("reverted content retains an empty-diff entry", RevertedContent),
    ("queries preserve refs, index, worktree and objects", ReadOnlyQuery),
    ("non-contiguous selected paths and skipped same-file edits", NonContiguous),
    ("rename chain", RenameChain),
    ("add then delete cancels", AddDelete),
    ("delete then add retains two changes", DeleteAdd),
    ("modify then delete", ModifyDelete),
    ("rename then delete", RenameDelete),
    ("rename then old path recreated", RenameRecreate),
    ("combined merge excludes unchanged-parent paths", Merge),
    ("conflict-resolved merge uses first-parent endpoints", ConflictMerge),
    ("added, deleted and renamed diff arguments execute", DiffArguments),
    ("literal wildcard and magic-looking pathspecs", LiteralPaths),
    ("SHA-256 addition and deletion diff endpoints", Sha256),
    ("submodule changes ignore user suppression config", Submodules),
    ("NUL-safe tabs, newlines, Unicode and quotes", OddPaths),
    ("binary changes and executable modes", BinaryAndMode),
    ("same-path aggregate respects binary Git attributes", BinaryAttributes),
    ("symlink type changes", TypeChange),
    ("add-rename-delete cancels", AddRenameDelete),
    ("malformed raw diff is rejected", Malformed),
    ("pre-cancelled selection launches no process", PreCancelled),
    ("in-flight cancellation terminates process", InFlightCancellation),
    ("Git failures are reported rather than empty success", Failure),
    ("actual XAML wires enabled detail tabs and explicit comparison", DetailTabWiring),
    ("VM selected commits and all three detail tabs", ViewModelTabs),
    ("VM rapid revision navigation and disposal", ViewModelRevisionNavigation),
    ("VM uses latest filter and clears hidden diff", ViewModelFilter),
    ("VM ignores disposed selection queued completion", ViewModelStale),
    ("VM errors and disposed error suppression", ViewModelError),
};
var failures = 0;
foreach (var test in tests)
{
    try
    { await test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures++; Console.Error.WriteLine($"FAIL {test.Name}: {ex}"); }
}
Console.WriteLine($"{tests.Length - failures}/{tests.Length} passed");
return failures == 0 ? 0 : 1;

static void Check(bool condition, string message)
{
    if (!condition)
        throw new Exception(message);
}
static CommitChange One(List<CommitChange> changes)
{
    Check(changes.Count == 1, $"Expected 1 change, got {changes.Count}");
    return changes[0];
}
static async Task Root()
{
    using var r = new Repo();
    r.Write("first", "hello\n");
    var root = r.Commit();
    var c = One(await r.Query(root));
    Check(c.Index == ChangeState.Added && string.IsNullOrEmpty(c.BeforeRevision) && c.AfterRevision == root.SHA && c.AfterPath == "first", "Root addition endpoints");
}
static async Task Contiguous()
{
    using var r = new Repo();
    r.Write("f", "one\n");
    var root = r.Commit();
    r.Write("f", "two\n");
    var a = r.Commit();
    r.Write("f", "three\n");
    var b = r.Commit();
    var c = One(await r.Query(b, a));
    Check(c.BeforeRevision == a.Parents[0] && c.AfterRevision == b.SHA && c.Index == ChangeState.Modified, "Oldest-before/newest-after order");
    Check(r.Read(c.BeforeRevision, c.BeforePath) == "one\n" && r.Read(c.AfterRevision, c.AfterPath) == "three\n", "Aggregated endpoint content");
}
static async Task RevertedContent()
{
    using var r = new Repo();
    r.Write("f", "original\n");
    r.Commit();
    r.Write("f", "changed\n");
    var first = r.Commit();
    r.Write("f", "original\n");
    var last = r.Commit();
    var change = One(await r.Query(last, first));
    Check(change.Index == ChangeState.Modified && change.BeforeObject == change.AfterObject,
        "A reverted file remains a selected changed path with identical endpoints");
    Check(r.Diff(new DiffOption(change)).Length == 0, "Reverted endpoints have an empty content diff");
}
static async Task ReadOnlyQuery()
{
    using var r = new Repo();
    r.Write("f", "original\n");
    var first = r.Commit();
    r.Write("f", "committed\n");
    var last = r.Commit();
    r.Write("f", "staged\n");
    r.Git("add", "f");
    r.Write("f", "unstaged\n");
    r.Write("untracked", "untracked\n");
    var head = r.Git("rev-parse", "HEAD");
    var refs = r.Git("show-ref");
    var index = File.ReadAllBytes(System.IO.Path.Combine(r.Path, ".git", "index"));
    var objects = r.Git("count-objects", "-v");
    var change = One(await r.Query(last, first));
    Check(r.Diff(new DiffOption(change)).Contains("+committed"), "Historical diff ignores dirty worktree content");
    Check(r.Git("rev-parse", "HEAD") == head && r.Git("show-ref") == refs, "Query changed HEAD or refs");
    Check(File.ReadAllBytes(System.IO.Path.Combine(r.Path, ".git", "index")).SequenceEqual(index), "Query changed the index");
    Check(File.ReadAllText(System.IO.Path.Combine(r.Path, "f")) == "unstaged\n" &&
        File.ReadAllText(System.IO.Path.Combine(r.Path, "untracked")) == "untracked\n", "Query changed working files");
    Check(r.Git("count-objects", "-v") == objects, "Query wrote temporary objects");
}
static async Task NonContiguous()
{
    using var r = new Repo();
    r.Write("f", "base\n");
    r.Commit();
    r.Write("f", "base\nselected-a\n");
    var a = r.Commit();
    r.Write("f", "base\nselected-a\nskipped\n");
    r.Write("unselected-only", "excluded\n");
    r.Commit();
    r.Write("f", "base\nselected-a\nskipped\nselected-b\n");
    var b = r.Commit();
    var c = One(await r.Query(b, a));
    Check(c.Path == "f" && r.Read(c.AfterRevision, c.AfterPath).Contains("skipped"), "Zipped endpoint semantics, not replay or range file list");
}
static async Task RenameChain()
{
    using var r = new Repo();
    r.Write("a", "stable contents\n");
    r.Commit();
    r.Git("mv", "a", "b");
    var a = r.Commit();
    r.Git("mv", "b", "c");
    var b = r.Commit();
    var c = One(await r.Query(b, a));
    Check(c.Index == ChangeState.Renamed && c.BeforePath == "a" && c.AfterPath == "c" && c.OriginalPath == "a", "Rename chain endpoints");
}
static async Task AddDelete()
{
    using var r = new Repo();
    r.Write("anchor", "a");
    r.Commit();
    r.Write("f", "added");
    var a = r.Commit();
    r.Delete("f");
    var b = r.Commit();
    Check((await r.Query(b, a)).Count == 0, "Add/delete should cancel");
}
static async Task DeleteAdd()
{
    using var r = new Repo();
    r.Write("f", "old");
    r.Commit();
    r.Delete("f");
    var a = r.Commit();
    r.Write("f", "new");
    var b = r.Commit();
    var changes = await r.Query(b, a);
    Check(changes.Count == 2 && changes[0].Index == ChangeState.Deleted && changes[1].Index == ChangeState.Added, "Delete/add must not merge distinct lifetimes");
}
static async Task ModifyDelete()
{
    using var r = new Repo();
    r.Write("f", "old");
    r.Commit();
    r.Write("f", "new");
    var a = r.Commit();
    r.Delete("f");
    var b = r.Commit();
    var c = One(await r.Query(b, a));
    Check(c.Index == ChangeState.Deleted && c.BeforeRevision == a.Parents[0] && string.IsNullOrEmpty(c.AfterRevision), "Modify/delete endpoints");
}
static async Task RenameDelete()
{
    using var r = new Repo();
    r.Write("a", "old");
    r.Commit();
    r.Git("mv", "a", "b");
    var a = r.Commit();
    r.Delete("b");
    var b = r.Commit();
    var c = One(await r.Query(b, a));
    Check(c.Index == ChangeState.Deleted && c.Path == "a", "Rename/delete keeps original path");
}
static async Task RenameRecreate()
{
    using var r = new Repo();
    r.Write("a", "stable");
    r.Commit();
    r.Git("mv", "a", "b");
    var a = r.Commit();
    r.Write("a", "recreated");
    var b = r.Commit();
    r.Write("b", "changed");
    var c = r.Commit();
    var changes = await r.Query(c, b, a);
    Check(changes.Count == 2 && changes.Any(x => x.Index == ChangeState.Added && x.Path == "a") && changes.Any(x => x.Index == ChangeState.Renamed && x.Path == "b"), "Do not confuse source path re-use with rename destination");
}
static async Task Merge()
{
    using var r = new Repo();
    r.Write("base", "base");
    r.Commit();
    r.Git("checkout", "-b", "side");
    r.Write("side", "side");
    r.Commit();
    r.Git("checkout", "main");
    r.Write("main", "main");
    r.Commit();
    r.Git("merge", "--no-ff", "side", "-m", "merge");
    var merge = r.Head();
    Check((await r.Query(merge)).Count == 0, "Combined merge excludes paths equal to either parent");
}
static async Task ConflictMerge()
{
    using var r = new Repo();
    r.Write("f", "base\n");
    r.Commit();
    r.Git("checkout", "-b", "side");
    r.Write("f", "side\n");
    r.Commit();
    r.Git("checkout", "main");
    r.Write("f", "main\n");
    var firstParent = r.Commit();
    try
    { r.Git("merge", "--no-ff", "side", "-m", "merge"); throw new Exception("Expected fixture conflict"); }
    catch (Exception ex) when (ex.Message.Contains("CONFLICT")) { }
    r.Write("f", "resolved\n");
    var merge = r.Commit();
    var c = One(await r.Query(merge));
    Check(c.Path == "f" && c.BeforeRevision == firstParent.SHA && c.AfterRevision == merge.SHA, "Combined merge retains resolution against first parent");
}
static async Task DiffArguments()
{
    using var r = new Repo();
    r.Write("odd name", "original\n");
    var added = r.Commit();
    var add = One(await r.Query(added));
    var option = new DiffOption(add);
    Check(!option.ToString().Contains(new string('0', 40)) && option.ToString().StartsWith(EmptyTreeHash.Guess(added.SHA)), "Addition uses empty tree, not zero blob");
    Check(r.Diff(option).Contains("+original"), "Addition diff executes");
    r.Git("mv", "odd name", "renamed");
    var renamed = r.Commit();
    var rename = One(await r.Query(renamed));
    option = new DiffOption(rename);
    Check(option.ToString() == $"{rename.BeforeObject} {rename.AfterObject}", "Rename compares exact blob endpoints");
    Check(r.Diff(option).Length == 0, "Pure rename has no content changes");
    r.Delete("renamed");
    var deleted = r.Commit();
    var delete = One(await r.Query(deleted));
    option = new DiffOption(delete);
    Check(option.ToString().Contains(EmptyTreeHash.Guess(deleted.SHA)) && !option.ToString().Contains(new string('0', 40)), "Deletion uses correct hash-format empty tree");
    Check(r.Diff(option).Contains("-original"), "Deletion diff executes");
}
static async Task LiteralPaths()
{
    if (OperatingSystem.IsWindows())
        return; // These filenames are not valid on Windows.
    using var r = new Repo();
    string[] names = ["*.txt", ":(glob)*", "unrelated.txt"];
    foreach (var name in names)
        r.Write(name, "content-for-" + name + "\n");
    var added = r.Commit();
    var changes = await r.Query(added);
    foreach (var name in names.Take(2))
    {
        var patch = r.Diff(new DiffOption(changes.Single(c => c.Path == name)));
        Check(patch.Contains("+content-for-" + name) && !patch.Contains("content-for-unrelated.txt"), "Literal addition path leaked another file");
    }
    foreach (var name in names)
        r.Delete(name);
    var deleted = r.Commit();
    changes = await r.Query(deleted);
    foreach (var name in names.Take(2))
    {
        var patch = r.Diff(new DiffOption(changes.Single(c => c.Path == name)));
        Check(patch.Contains("-content-for-" + name) && !patch.Contains("content-for-unrelated.txt"), "Literal deletion path leaked another file");
    }
}
static async Task Sha256()
{
    using var r = new Repo(true);
    r.Write("f", "sha256\n");
    var added = r.Commit();
    var c = One(await r.Query(added));
    Check(added.SHA.Length == 64 && r.Diff(new DiffOption(c)).Contains("+sha256"), "SHA256 root diff");
    r.Delete("f");
    var deleted = r.Commit();
    c = One(await r.Query(deleted));
    Check(r.Diff(new DiffOption(c)).Contains("-sha256"), "SHA256 delete diff");
}
static async Task Submodules()
{
    using var r = new Repo();
    r.Write("anchor", "one");
    var target = r.Commit();
    r.Git("update-index", "--add", "--cacheinfo", "160000," + target.SHA + ",lib");
    r.Git("commit", "-m", "add gitlink");
    var added = r.Head();
    r.Git("config", "diff.ignoreSubmodules", "all");
    r.Git("config", "submodule.lib.ignore", "all");
    var c = One(await r.Query(added));
    Check(c.Path == "lib" && c.AfterMode == "160000" && c.AfterObject == target.SHA, "Submodule hidden by config");
}
static async Task OddPaths()
{
    using var r = new Repo();
    var paths = new List<string> { "space name", "中文.txt", "--leading-option" };
    if (!OperatingSystem.IsWindows())
        paths.AddRange(["tab\tname", "line\nname", "quote\"name", "trailing\\", "back\\\"quote", "a*", "abcdef", ":(glob)*"]);
    foreach (var p in paths)
        r.Write(p, "contents " + p);
    var root = r.Commit();
    var changes = await r.Query(root);
    Check(changes.Select(c => c.Path).ToHashSet().SetEquals(paths), "NUL parsing must preserve exact paths");
    foreach (var change in changes)
    {
        var diff = r.Diff(new DiffOption(change));
        Check(diff.Contains("+contents " + change.Path.Replace("\n", "\n+")), "Added odd path contents missing: " + change.Path);
        Check(diff.Split("diff --git").Length == 2, "Added odd path diff included other files: " + change.Path);
    }

    if (!OperatingSystem.IsWindows())
    {
        r.Git("mv", "--", "tab\tname", "renamed\n中文");
        var rename = r.Commit();
        var c = One(await r.Query(rename));
        Check(c.BeforePath == "tab\tname" && c.AfterPath == "renamed\n中文", "NUL rename paths");
        // Restore the path so deletion assertions use the original file contents.
        r.Git("mv", "--", "renamed\n中文", "tab\tname");
        r.Commit();
    }

    foreach (var p in paths)
        r.Delete(p);
    var deleted = r.Commit();
    foreach (var change in await r.Query(deleted))
    {
        var diff = r.Diff(new DiffOption(change));
        Check(diff.Contains("-contents " + change.Path.Replace("\n", "\n-")), "Deleted odd path contents missing: " + change.Path);
        Check(diff.Split("diff --git").Length == 2, "Deleted odd path diff included other files: " + change.Path);
    }
}
static async Task BinaryAndMode()
{
    using var r = new Repo();
    r.Write("binary", "one\0two");
    r.Commit();
    r.Write("binary", "one\0three");
    var binary = r.Commit();
    var c = One(await r.Query(binary));
    Check(c.BeforeObject != c.AfterObject && c.Index == ChangeState.Modified, "Binary object endpoints");
    r.Git("update-index", "--chmod=+x", "binary");
    r.Git("commit", "-m", "executable mode");
    var mode = r.Head();
    c = One(await r.Query(mode));
    Check(c.BeforeMode == "100644" && c.AfterMode == "100755" && c.BeforeObject == c.AfterObject, "Mode-only change retained");
}
static async Task BinaryAttributes()
{
    using var r = new Repo();
    r.Write(".gitattributes", "*.dat -diff\n");
    r.Write("file.dat", "base ASCII contents\n");
    r.Commit();
    r.Write("file.dat", "first selected ASCII contents\n");
    var first = r.Commit();
    r.Write("file.dat", "skipped ASCII contents\n");
    r.Commit();
    r.Write("file.dat", "last selected ASCII contents\n");
    var last = r.Commit();
    var change = One(await r.Query(last, first));
    Check(change.BeforeRevision == first.Parents[0] && change.AfterRevision == last.SHA, "Attribute fixture uses aggregate endpoints");
    var diff = r.Diff(new DiffOption(change));
    Check(diff.Contains("Binary files ") && !diff.Contains("@@"), "Same-path ASCII -diff attribute must remain binary");
    Check(diff.Split("diff --git").Length == 2, "Attribute diff must include only selected path");
}
static async Task TypeChange()
{
    if (OperatingSystem.IsWindows())
        return;
    using var r = new Repo();
    r.Write("f", "contents");
    r.Commit();
    r.Delete("f");
    File.CreateSymbolicLink(System.IO.Path.Combine(r.Path, "f"), "target");
    var typeChange = r.Commit();
    var c = One(await r.Query(typeChange));
    Check(c.Index == ChangeState.TypeChanged && c.BeforeMode == "100644" && c.AfterMode == "120000", "File/symlink type change retained");
}
static async Task AddRenameDelete()
{
    using var r = new Repo();
    r.Write("anchor", "stable");
    r.Commit();
    r.Write("a", "contents");
    var a = r.Commit();
    r.Git("mv", "a", "b");
    var b = r.Commit();
    r.Delete("b");
    var c = r.Commit();
    Check((await r.Query(c, b, a)).Count == 0, "Add/rename/delete lifetime cancels");
}
static Task Malformed()
{
    try
    { QuerySelectedCommitChanges.Parse("not-a-header\0path\0", "before", "after"); throw new Exception("Malformed output accepted"); }
    catch (InvalidDataException) { }
    return Task.CompletedTask;
}
static async Task PreCancelled()
{
    using var r = new Repo();
    r.Write("f", "f");
    var c = r.Commit();
    using var cancel = new CancellationTokenSource();
    cancel.Cancel();
    var count = Command.StartInfoCount;
    try
    { await new QuerySelectedCommitChanges(r.Path).ReadAsync([c], cancel.Token); throw new Exception("Cancellation ignored"); }
    catch (OperationCanceledException) { }
    Check(Command.StartInfoCount == count, "Cancelled query started Git");
}
static async Task InFlightCancellation()
{
    if (OperatingSystem.IsWindows())
        return;
    using var r = new Repo();
    r.Write("f", "f");
    var c = r.Commit();
    Command.StartInfoOverride = () => new ProcessStartInfo("sh", "-c \"sleep 30\"") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
    try
    {
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        var timer = Stopwatch.StartNew();
        try
        { await new QuerySelectedCommitChanges(r.Path).ReadAsync([c], cancel.Token); throw new Exception("Cancellation ignored"); }
        catch (OperationCanceledException) { }
        Check(timer.Elapsed < TimeSpan.FromSeconds(5), "Cancelled process remained alive");
    }
    finally { Command.StartInfoOverride = null; }
}
static async Task Failure()
{
    using var r = new Repo();
    try
    { await r.Query(new Commit { SHA = new string('1', 40) }); throw new Exception("Git failure treated as success"); }
    catch (InvalidOperationException ex) { Check(ex.Message.Length > 0, "Missing Git error"); }
}

static async Task AwaitDispatch()
{
    var timer = Stopwatch.StartNew();
    while (Avalonia.Threading.Dispatcher.UIThread.PendingCount == 0)
    {
        if (timer.Elapsed > TimeSpan.FromSeconds(5))
            throw new Exception("Timed out awaiting queued UI callback");
        await Task.Delay(10);
    }
}
// Source-structure assertions complement (not replace) Avalonia compilation and
// runtime UI testing: VM-only tests cannot catch a disabled or unbound tab.
static Task DetailTabWiring()
{
    var root = new DirectoryInfo(AppContext.BaseDirectory);
    while (root != null && !File.Exists(System.IO.Path.Combine(root.FullName, "src", "Views", "Histories.axaml")))
        root = root.Parent;
    Check(root != null, "Could not locate production Histories.axaml from test output directory");
    var views = System.IO.Path.Combine(root.FullName, "src", "Views");
    var document = XDocument.Load(System.IO.Path.Combine(views, "Histories.axaml"));
    XNamespace av = "https://github.com/avaloniaui";
    XNamespace v = "using:SourceGit.Views";
    var template = document.Descendants(av + "DataTemplate")
        .Single(e => (string)e.Attribute("DataType") == "vm:CommitChanges");
    var tabs = template.Element(av + "TabControl");
    Check((string)tabs?.Attribute("SelectedIndex") == "{Binding ActiveTabIndex, Mode=TwoWay}", "Detail tab selection must bind to VM rather than fixed index");
    var items = tabs.Elements(av + "TabItem").ToArray();
    Check(items.Length == 3 && items.All(e => e.Attribute("IsEnabled") == null || (string)e.Attribute("IsEnabled") == "True"), "All three multi-selection detail tabs must be enabled");
    var headers = new[] { "Info", "Changes", "Files" };
    for (var i = 0; i < items.Length; i++)
    {
        var header = items[i].Element(av + "TabItem.Header");
        Check(header.Descendants(av + "TextBlock").Any(e => (string)e.Attribute("Text") == "{DynamicResource Text.CommitDetail." + headers[i] + "}"), "Detail tab order/header changed");
        Check(header.Descendants(av + "Border").Any(e => (string)e.Attribute("PointerPressed") == "OnTabHeaderPointerPressed"), "Each tab must preserve expand-on-header behavior");
    }
    var list = items[0].Descendants(av + "ListBox").Single();
    Check((string)list.Attribute("ItemsSource") == "{Binding InformationDetails}" &&
        (string)list.Attribute("SelectedItem") == "{Binding SelectedInformationDetail, Mode=TwoWay}", "Information cards choose the file revision through their detail context");
    Check(list.Descendants(av + "VirtualizingStackPanel").Any(), "Cards must virtualize to defer offscreen information queries");
    var card = list.Descendants(v + "CommitInformation").Single();
    Check((string)card.Attribute("OpenChangesInNewWindow") == "True" &&
        (string)card.Parent.Attribute("BorderThickness") == "1", "Cards reuse full information within a visible border and open individual diffs separately");
    var single = XDocument.Load(System.IO.Path.Combine(views, "CommitDetail.axaml"));
    Check(single.Descendants(v + "CommitInformation").Count() == 1, "Single and multi commits must share the exact Information view");
    var info = XDocument.Load(System.IO.Path.Combine(views, "CommitInformation.axaml"));
    var baseInfo = info.Descendants(v + "CommitBaseInfo").Single();
    foreach (var field in new[] { "FullMessage", "SignInfo", "WebLinks" })
        Check((string)baseInfo.Attribute(field) == "{Binding " + field + "}", "Full single-commit information missing: " + field);
    Check(info.Descendants(av + "ListBox").Any(e => ((string)e.Attribute("ItemsSource"))?.Contains("Top100Changes") == true), "Full information includes the original changed-file preview");
    Check(items[1].Descendants(v + "SelectedCommitChanges").Count() == 1, "Middle tab must retain aggregate changed-file viewer");
    var chooser = items[2].Descendants(av + "ComboBox").Single();
    Check((string)chooser.Attribute("ItemsSource") == "{Binding Commits}" &&
        (string)chooser.Attribute("SelectedItem") == "{Binding SelectedCommit, Mode=TwoWay}", "Files chooser must share selected commit with Information");
    var content = items[2].Descendants(av + "ContentControl").Single();
    Check((string)content.Attribute("Content") == "{Binding RevisionDetail}" &&
        content.Descendants(av + "DataTemplate").Any(e => (string)e.Attribute("DataType") == "vm:CommitDetail" && e.Element(v + "RevisionFiles") != null), "Files must reuse the single-commit revision viewer with its own detail VM");
    Check(info.Descendants(av + "Button").Any(e => (string)e.Attribute("Click") == "OnOpenAllChanges"), "Truncated cards must expose their own complete Changes list without replacing the aggregate");
    var detailCode = File.ReadAllText(System.IO.Path.Combine(views, "CommitDetail.axaml.cs"));
    Check(detailCode.Contains("CommitInformation.CreateChangeContextMenu(this, change)"), "Changes-tab menus must use the attached detail control, not its detached Information tab");
    var infoCode = File.ReadAllText(System.IO.Path.Combine(views, "CommitInformation.axaml.cs"));
    Check(infoCode.Contains("TopLevel.GetTopLevel(owner)") && infoCode.Contains("owner.CopyTextAsync"), "Shared menus preserve active clipboard and file picker owner");
    var code = File.ReadAllText(System.IO.Path.Combine(views, "Histories.axaml.cs"));
    Check(System.Text.RegularExpressions.Regex.IsMatch(code,
        @"if \(selected\.Count == 2\)\s*\{\s*var compare = new MenuItem.*?SelectedCommitChanges\.CompareRevisions.*?compare\.Click.*?histories\.CompareSelectedRevisions\(\).*?menu\.Items\.Add\(compare\)",
        System.Text.RegularExpressions.RegexOptions.Singleline), "Explicit two-revision comparison must remain a separate two-selection context action");
    return Task.CompletedTask;
}
static async Task ViewModelTabs()
{
    using var r = new Repo();
    r.Write("first", "one");
    var first = r.Commit();
    r.Write("skipped", "skip");
    var skipped = r.Commit();
    r.Write("last", "two");
    var last = r.Commit();
    var dispatcher = Avalonia.Threading.Dispatcher.UIThread;
    dispatcher.Hold = true;
    try
    {
        var selection = new List<Commit> { last, first };
        using var vm = new SourceGit.ViewModels.CommitChanges(new() { FullPath = r.Path }, selection);
        selection.Clear();
        Check(vm.Commits.SequenceEqual(new[] { last, first }) && vm.CommitCount == 2, "Information lists precisely the selected commits in history order using a defensive snapshot");
        Check(!vm.Commits.Contains(skipped), "Information must not include skipped commits");
        Check(vm.SelectedCommit == last && vm.ActiveTabIndex == 1 && vm.RevisionDetail == null, "Default Changes tab does not eagerly load Files");
        vm.ActiveTabIndex = 0;
        Check(vm.ActiveTabIndex == 0 && vm.RevisionDetail == null, "Information tab is reachable without loading Files");
        Check(vm.InformationDetails.Select(d => d.Commit).SequenceEqual(vm.Commits), "Every selected commit has one full-information context in history order");
        Check(vm.InformationDetails.All(d => d.DefersInformationLoading && d.ActiveTabIndex == 0), "Offscreen cards defer heavy loading and never create diff contexts");
        Check(vm.InformationDetails.Select(d => d.InformationLoadGate).Distinct().Count() == 1 &&
            vm.InformationDetails[0].InformationLoadGate.CurrentCount == 2, "Card queries share a bounded concurrency gate");
        vm.SelectedInformationDetail = vm.InformationDetails[1];
        Check(vm.SelectedCommit == first && vm.RevisionDetail == null, "Selecting information card chooses revision while keeping Files lazy");
        vm.SelectedInformationDetail = null;
        vm.SelectedInformationDetail = new SourceGit.ViewModels.CommitDetail(new(), null) { Commit = skipped };
        Check(vm.SelectedCommit == first, "Null and outside-card selection cannot replace the chosen revision");
        vm.ActiveTabIndex = 2;
        var detail = vm.RevisionDetail;
        Check(vm.ActiveTabIndex == 2 && detail?.Commit == first, "Files uses the selected information row revision");
        vm.ActiveTabIndex = 1;
        Check(vm.ActiveTabIndex == 1 && vm.RevisionDetail == detail, "Changes tab preserves current file revision");
        vm.ActiveTabIndex = -1;
        vm.ActiveTabIndex = 3;
        Check(vm.ActiveTabIndex == 1, "Invalid tab indexes do not hide all content");
        await AwaitDispatch();
        dispatcher.Drain();
        Check(vm.TotalChanges == 2 && vm.VisibleChanges.Select(c => c.Path).Order().SequenceEqual(new[] { "first", "last" }), "Browsing a selected commit does not change the aggregate summary");
        Check(vm.Commits.SequenceEqual(new[] { last, first }), "Tab navigation preserves exact selected commit list");
    }
    finally { dispatcher.Hold = false; dispatcher.Drain(); }
}
static async Task ViewModelRevisionNavigation()
{
    using var r = new Repo();
    r.Write("first", "one");
    var first = r.Commit();
    r.Write("last", "two");
    var last = r.Commit();
    var dispatcher = Avalonia.Threading.Dispatcher.UIThread;
    dispatcher.Hold = true;
    try
    {
        using var vm = new SourceGit.ViewModels.CommitChanges(new() { FullPath = r.Path }, [last, first]);
        vm.ActiveTabIndex = 2;
        Check(vm.RevisionDetail?.Commit == last, "Files defaults to topmost selected history commit");
        for (var i = 0; i < 10; i++)
        {
            var previous = vm.RevisionDetail;
            var selected = i % 2 == 0 ? first : last;
            vm.SelectedCommit = selected;
            Check(previous.IsDisposed && vm.RevisionDetail != previous && vm.RevisionDetail.Commit == selected, "Revision navigation disposes old detail and creates a fresh revision owner");
            var current = vm.RevisionDetail;
            vm.SelectedCommit = selected;
            vm.SelectedCommit = null;
            vm.SelectedCommit = new Commit { SHA = new string('f', 40) };
            Check(vm.SelectedCommit == selected && vm.RevisionDetail == current && !current.IsDisposed, "Repeated, null and outside-selection row events do not replace current detail");
            vm.ActiveTabIndex = 0;
            vm.ActiveTabIndex = 1;
            vm.ActiveTabIndex = 2;
            Check(vm.RevisionDetail == current, "Repeated tab navigation does not reload the chosen revision");
        }
        await AwaitDispatch();
        var finalDetail = vm.RevisionDetail;
        var finalSelection = vm.SelectedCommit;
        vm.Dispose();
        vm.Dispose();
        dispatcher.Drain();
        Check(vm.InformationDetails.All(d => d.IsDisposed), "Disposal cancels every card's pending information queries");
        Check(finalDetail.IsDisposed && vm.TotalChanges == 0, "Disposal closes file detail and suppresses queued aggregate completion");
        vm.SelectedCommit = first;
        vm.ActiveTabIndex = 0;
        vm.ActiveTabIndex = 2;
        Check(vm.SelectedCommit == finalSelection && (vm.RevisionDetail == null || vm.RevisionDetail == finalDetail), "Late navigation cannot resurrect a disposed detail owner");
    }
    finally { dispatcher.Hold = false; dispatcher.Drain(); }
}
static async Task ViewModelFilter()
{
    using var r = new Repo();
    r.Write("first", "one");
    r.Write("second", "two");
    var commit = r.Commit();
    var dispatcher = Avalonia.Threading.Dispatcher.UIThread;
    dispatcher.Hold = true;
    try
    {
        using var vm = new SourceGit.ViewModels.CommitChanges(new() { FullPath = r.Path }, [commit]);
        vm.SearchFilter = "SECOND";
        await AwaitDispatch();
        dispatcher.Drain();
        Check(!vm.IsLoading && vm.TotalChanges == 2 && vm.VisibleChanges.Count == 1 && vm.VisibleChanges[0].Path == "second", "Latest filter applies after async load");
        Check(vm.DiffContext?.Option.Path == "second", "Auto-select visible diff");
        vm.SearchFilter = "missing";
        Check(vm.VisibleChanges.Count == 0 && vm.ChangeSelection.Count == 0 && vm.DiffContext == null, "Hiding selection clears stale diff");
        vm.ClearSearchFilter();
        Check(vm.VisibleChanges.Count == 2 && vm.ChangeSelection.Count == 1, "Clearing filter restores changes");
        vm.ChangeSelection = new(vm.VisibleChanges);
        Check(vm.DiffContext == null, "Multiple file selection has no stale single-file diff");
    }
    finally { dispatcher.Hold = false; dispatcher.Drain(); }
}
static async Task ViewModelStale()
{
    using var r = new Repo();
    r.Write("old", "one");
    var old = r.Commit();
    r.Write("new", "two");
    var current = r.Commit();
    var dispatcher = Avalonia.Threading.Dispatcher.UIThread;
    dispatcher.Hold = true;
    try
    {
        var selection = new List<Commit> { old };
        using var oldVm = new SourceGit.ViewModels.CommitChanges(new() { FullPath = r.Path }, selection);
        selection.Clear(); // Constructor owns a snapshot, not the mutable UI list.
        await AwaitDispatch();
        oldVm.Dispose();
        dispatcher.Drain();
        Check(oldVm.TotalChanges == 0 && oldVm.DiffContext == null, "Disposed success callback populated stale VM");
        using var currentVm = new SourceGit.ViewModels.CommitChanges(new() { FullPath = r.Path }, [current]);
        await AwaitDispatch();
        dispatcher.Drain();
        Check(currentVm.CommitCount == 1 && currentVm.VisibleChanges.Single().Path == "new", "Newest selection owns result");
    }
    finally { dispatcher.Hold = false; dispatcher.Drain(); }
}
static async Task ViewModelError()
{
    using var r = new Repo();
    var invalid = new Commit { SHA = new string('1', 40) };
    var dispatcher = Avalonia.Threading.Dispatcher.UIThread;
    dispatcher.Hold = true;
    try
    {
        using var errorVm = new SourceGit.ViewModels.CommitChanges(new() { FullPath = r.Path }, [invalid]);
        await AwaitDispatch();
        dispatcher.Drain();
        Check(!errorVm.IsLoading && !string.IsNullOrEmpty(errorVm.Error), "Failed query did not expose error");
        using var staleVm = new SourceGit.ViewModels.CommitChanges(new() { FullPath = r.Path }, [invalid]);
        await AwaitDispatch();
        staleVm.Dispose();
        dispatcher.Drain();
        Check(string.IsNullOrEmpty(staleVm.Error), "Disposed error callback populated stale VM");
    }
    finally { dispatcher.Hold = false; dispatcher.Drain(); }
}

sealed class Repo : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sourcegit-selected-tests-" + Guid.NewGuid().ToString("N"));
    public Repo(bool sha256 = false)
    {
        Directory.CreateDirectory(Path);
        Git("init", "-b", "main", "--object-format=" + (sha256 ? "sha256" : "sha1"));
        Git("config", "user.name", "Test");
        Git("config", "user.email", "test@example.invalid");
        Git("config", "commit.gpgsign", "false");
    }
    public void Write(string path, string text) => File.WriteAllText(System.IO.Path.Combine(Path, path), text);
    public void Delete(string path) => File.Delete(System.IO.Path.Combine(Path, path));
    public Commit Commit() { Git("add", "-A"); Git("commit", "-m", "fixture"); return Head(); }
    public Commit Head()
    {
        var parts = Git("rev-list", "--parents", "-n", "1", "HEAD").Trim().Split(' ');
        return new Commit { SHA = parts[0], Parents = parts.Skip(1).ToList() };
    }
    public Task<List<CommitChange>> Query(params Commit[] commits) => new QuerySelectedCommitChanges(Path).ReadAsync(commits, CancellationToken.None);
    public string Read(string revision, string path) => Git("show", $"{revision}:{path}");
    public string Diff(DiffOption option)
    {
        var info = new ProcessStartInfo("git", "diff --no-ext-diff " + option) { WorkingDirectory = Path, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        using var process = Process.Start(info);
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new Exception($"git diff {option}: {stderr.Result}{stdout.Result}");
        return stdout.Result;
    }
    public string Git(params string[] args)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = Path, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in args)
            info.ArgumentList.Add(arg);
        using var process = Process.Start(info);
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new Exception($"git {string.Join(' ', args)}: {stderr.Result}{stdout.Result}");
        return stdout.Result;
    }
    public void Dispose()
    {
        // Git marks object files read-only on Windows. Only touch this fixture's
        // generated temporary directory, and never traverse reparse points.
        foreach (var file in Directory.EnumerateFiles(Path, "*", new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        }))
        {
            var attributes = File.GetAttributes(file);
            if ((attributes & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
        }
        Directory.Delete(Path, true);
    }
}
