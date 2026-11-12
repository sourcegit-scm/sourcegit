using System.Diagnostics;
using System.Text;

// Child mode: print how THIS process received its argv, one entry per line.
if (args.Length >= 1 && args[0] == "--print-argv")
{
    var sb = new StringBuilder();
    for (int i = 1; i < args.Length; i++)
        sb.Append('[').Append(args[i]).Append("]\n");
    Console.Out.Write(sb.ToString());
    return 0;
}

// Mirror of SourceGit's src/App.Extensions.cs StringExtensions.Quoted().
// Keep this in sync with that method.
static string Quoted(string value)
{
    var builder = new StringBuilder(value.Length + 2);
    builder.Append('"');
    var i = 0;
    while (i < value.Length)
    {
        var c = value[i++];
        if (c == '\\')
        {
            var backslashes = 1;
            while (i < value.Length && value[i] == '\\') { i++; backslashes++; }
            if (i == value.Length)
                builder.Append('\\', backslashes * 2);
            else if (value[i] == '"')
            {
                builder.Append('\\', backslashes * 2 + 1).Append('"');
                i++;
            }
            else
                builder.Append('\\', backslashes);
        }
        else if (c == '"') builder.Append('\\').Append('"');
        else builder.Append(c);
    }
    builder.Append('"');
    return builder.ToString();
}

var self = Environment.ProcessPath!;

// The invariant under test:
//   Quoted(name) must parse back to EXACTLY ONE argv entry equal to `name`.
// Benign filenames only -- no git, no options, no payload.
string[] names =
{
    "normal.txt",
    "with space.txt",
    "quote\".txt",
    @"back\slash.txt",
    @"trailing\",          // backslash immediately before the closing quote
    @"a\""b.txt",          // backslash then quote
    @"two\\",              // two trailing backslashes
    @"c:\dir\name.txt",    // Windows-style path
};

int failures = 0;
foreach (var name in names)
{
    var psi = new ProcessStartInfo
    {
        FileName = self,
        Arguments = $"--print-argv {Quoted(name)}",   // mirrors `show {rev}:{file.Quoted()}`
        UseShellExecute = false,
        RedirectStandardOutput = true,
    };
    using var p = Process.Start(psi)!;
    var outp = p.StandardOutput.ReadToEnd();
    p.WaitForExit();

    var received = new List<string>();
    foreach (var line in outp.Split('\n'))
        if (line.StartsWith('[') && line.EndsWith(']'))
            received.Add(line[1..^1]);

    bool ok = received.Count == 1 && received[0] == name;
    if (!ok) failures++;
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  input={Display(name)}");
    if (!ok)
    {
        Console.WriteLine($"      Arguments string = {Display(psi.Arguments)}");
        Console.WriteLine($"      parsed into {received.Count}: {string.Join(" | ", received.Select(Display))}");
    }
}

Console.WriteLine();
Console.WriteLine(failures == 0
    ? "All inputs round-tripped to the same single argument (Quoted is correct)."
    : $"{failures} input(s) did NOT round-trip: Quoted() produced a different argument than the input.");
return failures == 0 ? 0 : 1;

static string Display(string s) => "\u00ab" + s.Replace("\n","\\n") + "\u00bb";
