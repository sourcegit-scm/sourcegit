using System;
using System.IO;
using System.Linq;

namespace DevBoard.Native;

internal static class DataDirectoryResolver
{
    public static string Resolve(
        string portablePath,
        string devBoardPath,
        string legacySourceGitPath,
        Action<string>? log = null)
    {
        if (!string.IsNullOrWhiteSpace(portablePath) && Directory.Exists(portablePath))
            return portablePath;

        if (HasContent(devBoardPath))
            return devBoardPath;

        if (!Directory.Exists(legacySourceGitPath))
            return devBoardPath;

        var stagingPath = $"{devBoardPath}.migration-{Guid.NewGuid():N}";
        try
        {
            CopyDirectory(legacySourceGitPath, stagingPath);

            if (Directory.Exists(devBoardPath))
                Directory.Delete(devBoardPath, false);

            Directory.Move(stagingPath, devBoardPath);
            log?.Invoke("Migrated legacy SourceGit data to DevBoard."); // legacy-migration
            return devBoardPath;
        }
        catch (IOException)
        {
            CleanupStaging(stagingPath);
            log?.Invoke("Failed to migrate legacy SourceGit data; using legacy data for this run."); // legacy-migration
            return legacySourceGitPath;
        }
        catch (UnauthorizedAccessException)
        {
            CleanupStaging(stagingPath);
            log?.Invoke("Failed to migrate legacy SourceGit data; using legacy data for this run."); // legacy-migration
            return legacySourceGitPath;
        }
    }

    private static bool HasContent(string path)
    {
        return Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any();
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, directory);
            Directory.CreateDirectory(Path.Combine(destination, relative));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: false);
        }
    }

    private static void CleanupStaging(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch
        {
            // Best effort only. The incomplete staging directory is never treated as live data.
        }
    }
}
