using System.Collections.Generic;

namespace DevBoard.Tests;

public sealed record DevSpacesScreenshotScenario(
    string Id,
    string Title,
    string Category,
    IReadOnlyList<string> SourcePaths);

public static class DevSpacesScreenshotCatalog
{
    public static IReadOnlyList<DevSpacesScreenshotScenario> All { get; } =
    [
        new("terminal-main", "DevSpaces terminal", "terminal", ["src/Views/DevSpaces.axaml"]),
        new("terminal-picker", "DevSpaces terminal picker", "terminal", ["src/Views/DevSpaces.axaml"]),
        new("terminal-profiles", "DevSpace terminal profiles", "terminal", ["src/Views/DevSpaceProfiles.axaml", "src/ViewModels/DevSpaceProfiles.cs"]),
        new("profile-validation", "DevSpace profile validation", "terminal", ["src/Views/DevSpaceProfiles.axaml", "src/ViewModels/DevSpaceProfiles.cs"]),
        new("files-explorer", "DevSpaces Files explorer", "files", ["src/Views/DevSpacesFiles.axaml", "src/ViewModels/DevSpacesFiles.cs"]),
        new("files-diff", "DevSpaces modified file preview", "files", ["src/Views/DevSpacesFiles.axaml", "src/ViewModels/DevSpacesFiles.cs"]),
        new("files-statuses", "DevSpaces file statuses", "files", ["src/Views/DevSpacesFiles.axaml", "src/ViewModels/DevSpacesFiles.cs"]),
        new("ctrl-p-go-to-file", "Ctrl+P Go to File", "navigation", ["src/Views/Launcher.axaml", "src/ViewModels/GoToFile.cs"]),
        new("files-terminal-switch", "Files and terminal switching", "workspace", ["src/Views/DevSpaces.axaml", "src/ViewModels/DevSpaces.cs"]),
        new("per-tab-state", "DevSpaces per-tab state", "workspace", ["src/ViewModels/LauncherPage.cs", "src/ViewModels/DevSpaces.cs"]),
        new("worktree-base-badges", "Worktree base branch badges", "workspace", ["src/Views/Launcher.axaml", "src/ViewModels/LauncherPage.cs"]),
    ];
}
