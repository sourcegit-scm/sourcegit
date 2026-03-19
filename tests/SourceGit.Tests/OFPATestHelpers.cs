using System.Diagnostics;

namespace SourceGit.Tests;

internal static class OFPATestHelpers
{
    public static string GetTestDataPath(string relativePath)
    {
        return Path.Combine(AppContext.BaseDirectory, "TestData", relativePath);
    }

    public static string CreateTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "SourceGit.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string CreateRepository()
    {
        var repoDir = CreateTempDirectory();
        RunGit(repoDir, "init");
        RunGit(repoDir, "config user.name \"SourceGit Tests\"");
        RunGit(repoDir, "config user.email tests@example.com");
        return repoDir;
    }

    public static void CopyTestAsset(string repositoryPath, string sourceRelativePath, string destinationRelativePath)
    {
        var source = GetTestDataPath(sourceRelativePath);
        var destination = Path.Combine(repositoryPath, destinationRelativePath);
        var parent = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);

        File.Copy(source, destination, true);
    }

    public static string RunGit(string workingDirectory, string arguments)
    {
        var gitExecutable = string.IsNullOrEmpty(Native.OS.GitExecutable) ? "git" : Native.OS.GitExecutable;
        var starter = new ProcessStartInfo
        {
            WorkingDirectory = workingDirectory,
            FileName = gitExecutable,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var proc = Process.Start(starter)!;
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();

        Assert.True(proc.ExitCode == 0, $"git {arguments} failed.\nstdout: {stdout}\nstderr: {stderr}");
        return stdout.Trim();
    }

    public static void DeleteDirectory(string path)
    {
        try
        {
            if (!Directory.Exists(path))
                return;

            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);

            Directory.Delete(path, true);
        }
        catch
        {
            // Best-effort cleanup for temp repositories on Windows.
        }
    }
}
