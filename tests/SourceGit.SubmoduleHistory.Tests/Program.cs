using System.Diagnostics;
using SourceGit.Commands;
using SourceGit.Models;
using Commit = SourceGit.Models.Commit;

var temp = Path.Combine(Path.GetTempPath(), "sourcegit-history-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
var checks = 0;
try
{
    var child = Init("child");
    var a = CommitFile(child, "base.txt", "base");
    var b = CommitFile(child, "next.txt", "next");
    Git(child, "checkout", "-b", "side", a);
    var side = CommitFile(child, "side.txt", "side");
    Git(child, "checkout", "master");
    Git(child, "merge", "--no-ff", "side", "-m", "merge child");
    var merged = Git(child, "rev-parse", "HEAD");
    var root = Init("parent");
    CommitFile(root, "root.txt", "root");
    Git(root, "-c", "protocol.file.allow=always", "submodule", "add", child, "modules/with space");
    Git(root, "-c", "protocol.file.allow=always", "submodule", "add", child, "second");
    var module = Path.Combine(root, "modules/with space");
    Git(module, "checkout", a);
    Git(Path.Combine(root, "second"), "checkout", a);
    Git(root, "add", ".");
    Git(root, "commit", "-m", "add modules");
    var initial = ReadCommit(root);
    Git(module, "checkout", merged);
    Git(Path.Combine(root, "second"), "checkout", merged);
    Git(root, "add", ".");
    Git(root, "commit", "-m", "update modules");
    var update = ReadCommit(root);
    var query = new QuerySubmoduleHistory(root);
    var sections = await query.GetChangesAsync(update);
    Check(sections.Count == 2, "discover both direct gitlinks, including a path with spaces");
    Check(sections.All(x => x.Previous[a] == initial.SHA), "find previous superproject change anchors");
    foreach (var section in sections)
        await query.LoadAsync(section, SubmoduleHistoryMode.ChangedRange, 100);
    var first = sections[0];
    Check(first.Error.Length == 0, "load initialized submodule");
    Check(first.Commits.Select(x => x.SHA).ToHashSet().SetEquals([a, b, side, merged]), "range includes both merge parents and old boundary");
    Check(first.Commits.Single(x => x.SHA == merged).Parents.Count == 2, "retain real merge ancestry");
    var rows = SubmoduleHistory.Compose([update, initial], sections);
    Check(rows.Select(x => x.GraphKey).Distinct().Count() == rows.Count, "same SHA in different modules has unique graph identity");
    Check(update.GraphParents == null && update.Parents.Count == 1, "composition does not mutate source commits");
    Check(rows.Any(x => x.SubmoduleHistory != null && x.DisplayParents.Contains(initial.SHA)), "old pointer reconnects to previous superproject change");
    CommitGraph.SetDefaultPens();
    Check(CommitGraph.Generate(rows, false, CommitGraphHighlighting.All, []).Dots.Count == rows.Count, "layout renders all root and submodule rows");
    await query.LoadAsync(first, SubmoduleHistoryMode.FullHistory, 1);
    Check(first.IsTruncated && first.Commits.Count == 1, "large full history reports truncation");
    await query.LoadAsync(first, SubmoduleHistoryMode.FullHistory, 100);
    Check(first.Commits.Any(x => x.SHA == a), "full history reaches root");
    Check(first.Commits[0].SHA == merged, "full history uses topological order");
    Git(module, "checkout", b);
    Git(root, "add", "."); Git(root, "commit", "-m", "rewind");
    var rewind = (await query.GetChangesAsync(ReadCommit(root))).Single();
    await query.LoadAsync(rewind, SubmoduleHistoryMode.ChangedRange, 100);
    Check(rewind.Commits.Any(x => x.SHA == b) && rewind.Commits.Any(x => x.SHA == merged), "rewind retains new and old pointers");
    Git(module, "checkout", side);
    Git(root, "add", "."); Git(root, "commit", "-m", "diverge");
    var divergent = (await query.GetChangesAsync(ReadCommit(root))).Single();
    await query.LoadAsync(divergent, SubmoduleHistoryMode.ChangedRange, 100);
    Check(divergent.Commits.Any(x => x.SHA == side) && divergent.Commits.Any(x => x.SHA == b), "divergent update exposes both tips");
    Check(!divergent.Commits.Single(x => x.SHA == side).Parents.Contains(b), "never invent ancestry between divergent pointers");
    var unchangedSHA = CommitFile(root, "root.txt", "unrelated");
    Check((await query.GetChangesAsync(ReadCommit(root))).Count == 0, "unrelated commit does not expand submodules");
    var additions = await query.GetChangesAsync(initial);
    Check(additions.All(x => x.Previous.Count == 0), "submodule additions have no old boundary");
    Git(root, "rm", "-f", "second"); Git(root, "commit", "-m", "delete module");
    var deleted = (await query.GetChangesAsync(ReadCommit(root))).Single();
    Check(deleted.NewSHA == null && deleted.Previous.Count == 1, "detect removed gitlinks");
    await query.LoadAsync(deleted, SubmoduleHistoryMode.ChangedRange, 100);
    Check(deleted.Error == "SubmoduleHistory.Unavailable", "deleted/uninitialized module gives actionable unavailable status");
    var missing = new SubmoduleHistory { OwnerSHA = update.SHA, Path = "modules/with space", NewSHA = new string('1', 40) };
    await query.LoadAsync(missing, SubmoduleHistoryMode.FullHistory, 100);
    Check(missing.Error == "SubmoduleHistory.MissingObjects", "missing objects do not appear as empty success");
    var escape = new SubmoduleHistory { OwnerSHA = update.SHA, Path = "../child", NewSHA = a };
    await query.LoadAsync(escape, SubmoduleHistoryMode.FullHistory, 100);
    Check(escape.Error.Length > 0, "reject path traversal outside parent repository");
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    try { await new QuerySubmoduleHistory(root, cancellation.Token).GetChangesAsync(update); throw new Exception("cancellation ignored"); }
    catch (OperationCanceledException) { checks++; }
    var preferences = new SourceGit.ViewModels.Preferences();
    Check(preferences.MaxExpandedSubmoduleHistories == 3 && preferences.SubmoduleHistoryMode == SubmoduleHistoryMode.ChangedRange, "default preferences");
    preferences.MaxExpandedSubmoduleHistories = 0;
    Check(preferences.MaxExpandedSubmoduleHistories == 1, "validate lower expansion limit");
    preferences.MaxExpandedSubmoduleHistories = 999;
    Check(preferences.MaxExpandedSubmoduleHistories == 20, "validate upper expansion limit");
    Console.WriteLine($"PASS: {checks} integration assertions");
}
finally { Directory.Delete(temp, true); }

string Init(string name)
{
    var path = Path.Combine(temp, name);
    Directory.CreateDirectory(path);
    Git(path, "init", "-b", "master");
    Git(path, "config", "user.name", "SourceGit tests");
    Git(path, "config", "user.email", "tests@example.invalid");
    return path;
}
string CommitFile(string path, string name, string text)
{
    File.WriteAllText(Path.Combine(path, name), text);
    Git(path, "add", "."); Git(path, "commit", "-m", text);
    return Git(path, "rev-parse", "HEAD");
}
Commit ReadCommit(string path)
{
    var fields = Git(path, "log", "-1", "--format=%H %P").Split(' ', StringSplitOptions.RemoveEmptyEntries);
    return new Commit { SHA = fields[0], Parents = fields.Skip(1).ToList() };
}
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
    Console.WriteLine("PASS " + message);
}
string Git(string path, params string[] args)
{
    using var process = new Process { StartInfo = new ProcessStartInfo("git") { WorkingDirectory = path, RedirectStandardOutput = true, RedirectStandardError = true } };
    foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
    process.Start();
    var output = process.StandardOutput.ReadToEndAsync();
    var error = process.StandardError.ReadToEndAsync();
    process.WaitForExit();
    if (process.ExitCode != 0) throw new Exception(error.Result);
    return output.Result.Trim();
}
