using System.Diagnostics;
using System.Text;
using SourceGit.Commands;
using SourceGit.Models;
using Commit = SourceGit.Models.Commit;
using SourceGit.ViewModels;

internal static class Program
{
    private static int _checks;

    private static async Task Main()
    {
        var fixture = Path.Combine(Path.GetTempPath(), "sourcegit-mixed-search-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        SourceGit.Native.OS.GitExecutable = "git";
        try
        {
            Git(fixture, "init", "-b", "main");
            Git(fixture, "config", "user.name", "Alice Shared");
            Git(fixture, "config", "user.email", "alice@example.com");
            Git(fixture, "commit", "--allow-empty", "-m", "Alpha Subject Shared", "-m", "Body-only needle\nLiteral [brackets]. and a \"quote\"\nUnicode: árvíztűrő");
            Git(fixture, "commit", "--amend", "--allow-empty", "-m", "Alpha Subject Shared", "-m", "Body-only needle\nLiteral [brackets]. and a \"quote\"\nUnicode: árvíztűrő\n" + new string('x', 9000) + "long-body needle");
            var first = Git(fixture, "rev-parse", "HEAD").Trim();
            Git(fixture, "config", "user.name", "Bob");
            Git(fixture, "config", "user.email", "bob@example.com");
            Git(fixture, "commit", "--allow-empty", "-m", "Shared second");
            var second = Git(fixture, "rev-parse", "HEAD").Trim();
            Git(fixture, "checkout", "-b", "feature");
            Git(fixture, "commit", "--allow-empty", "-m", "remote-only needle");
            var remote = Git(fixture, "rev-parse", "HEAD").Trim();
            Git(fixture, "update-ref", "refs/remotes/origin/feature", remote);
            Git(fixture, "checkout", "main");
            Git(fixture, "branch", "-D", "feature");

            Check(new SearchCommitContext(fixture, null).Method == (int)CommitSearchMethod.Mixed, "Mixed is the default mode");
            Check(!(await Search(fixture, first.Substring(9, 12).ToUpperInvariant())).Any(x => x.SHA == first), "interior SHA substring does not match");
            Check((await Search(fixture, first[..12].ToUpperInvariant())).Any(x => x.SHA == first), "case-insensitive SHA prefix");
            Check((await Search(fixture, first[..4])).Any(x => x.SHA == first), "short SHA prefix");
            Check((await Search(fixture, first)).Single().SHA == first, "full SHA");
            Check((await Search(fixture, "ALICE")).Single().SHA == first, "author name");
            Check((await Search(fixture, "BOB@EXAMPLE.COM")).Count == 2, "author email across local and remote branches");
            Check((await Search(fixture, "alpha subject")).Single().SHA == first, "subject match");
            Check((await Search(fixture, "body-only NEEDLE")).Single().SHA == first, "full-message body match");
            Check((await Search(fixture, "[brackets].")).Single().SHA == first, "punctuation is literal");
            Check((await Search(fixture, "\"quote\"")).Single().SHA == first, "quote is literal");
            Check((await Search(fixture, "ÁRVÍZTŰRŐ")).Single().SHA == first, "Unicode case-insensitive match");
            Check((await Search(fixture, "long-body needle")).Single().SHA == first, "message matching spans stream buffers");
            var shared = await Search(fixture, "shared");
            Check(shared.Count == 2 && shared.Any(x => x.SHA == first) && shared.Any(x => x.SHA == second), "author OR message matches");
            Check((await Search(fixture, "remote-only")).Single().SHA == remote, "remote branch scope");
            Check((await Search(fixture, "remote-only", true)).Count == 0, "current-branch scope excludes remote-only commits");
            Check((await Search(fixture, "nonexistent needle")).Count == 0, "no matches");
            Check((await Search(fixture, "")).Count == 0, "empty filter");
            Check((await new QueryCommits(fixture, "Alpha", CommitSearchMethod.ByMessage, true).GetResultAsync()).Single().SHA == first, "existing Message mode");
            Check((await new QueryCommits(fixture, "Alice", CommitSearchMethod.ByAuthor, true).GetResultAsync()).Single().SHA == first, "existing Author mode");

            Git(fixture, "checkout", "--detach");
            Git(fixture, "commit", "--allow-empty", "-m", "detached-only needle");
            Check((await Search(fixture, "detached-only")).Count == 1, "all-branches scope includes detached HEAD");
            Check((await Search(fixture, "detached-only", true)).Count == 1, "current-branch scope includes detached HEAD");
            Git(fixture, "checkout", "main");
            ImportBulk(fixture, second);
            Check((await Search(fixture, "Alpha Subject")).Single().SHA == first, "message match older than newest 1000 commits");
            Check((await Search(fixture, first[..12])).Any(x => x.SHA == first), "SHA prefix match older than newest 1000 commits");
            var bulk = await Search(fixture, "bulk-match", true);
            Check(bulk.Count == 1000 && bulk[0].Subject == "bulk-match 1004" && bulk[^1].Subject == "bulk-match 5", "limit applies to matches in history order");
            Check(bulk.Select(x => x.SHA).Distinct().Count() == bulk.Count, "results contain no duplicates");
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            Check((await new QueryCommits(fixture, "bulk", CommitSearchMethod.Mixed, false) { CancellationToken = cancelled.Token }.GetResultAsync()).Count == 0, "cancelled query exits without results");
            using var running = new CancellationTokenSource();
            var query = new QueryCommits(fixture, "not found", CommitSearchMethod.Mixed, false) { CancellationToken = running.Token }.GetResultAsync();
            running.Cancel();
            await query.WaitAsync(TimeSpan.FromSeconds(5));
            Check(true, "active query cancellation completes promptly");
            var unborn = Path.Combine(fixture, "unborn");
            Directory.CreateDirectory(unborn);
            Git(unborn, "init", "-b", "main");
            Check((await Search(unborn, "needle")).Count == 0, "repository without commits returns no results");
            Console.WriteLine($"PASS: {_checks} mixed-search checks");
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(fixture, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(fixture, true);
        }
    }

    private static Task<List<Commit>> Search(string fixture, string filter, bool current = false) =>
        new QueryCommits(fixture, filter, CommitSearchMethod.Mixed, current).GetResultAsync();

    private static void ImportBulk(string fixture, string parent)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = fixture, UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false) };
        start.ArgumentList.Add("fast-import");
        start.ArgumentList.Add("--quiet");
        using var process = Process.Start(start);
        for (var i = 0; i < 1005; i++)
        {
            var message = "bulk-match " + i;
            var from = i == 0 ? parent : ":" + i;
            process.StandardInput.Write($"commit refs/heads/main\nmark :{i + 1}\ncommitter Bulk <bulk@example.com> {1800000000 + i} +0000\ndata {Encoding.UTF8.GetByteCount(message)}\n{message}\nfrom {from}\n\n");
        }
        process.StandardInput.Close();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new Exception(process.StandardError.ReadToEnd());
    }

    private static string Git(string fixture, params string[] args)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = fixture, UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8 };
        foreach (var arg in args)
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start);
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new Exception(process.StandardError.ReadToEnd());
        return output;
    }

    private static void Check(bool condition, string name)
    {
        if (!condition)
            throw new Exception("FAIL: " + name);
        _checks++;
    }
}
