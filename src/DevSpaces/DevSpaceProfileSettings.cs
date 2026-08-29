using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SourceGit.DevSpaces
{
    public sealed class DevSpaceTerminalProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("D");
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;

        public DevSpaceTerminalProfile Clone(bool createNewId = false)
        {
            return new DevSpaceTerminalProfile
            {
                Id = createNewId ? Guid.NewGuid().ToString("D") : Id,
                Name = Name,
                Path = Path,
                Command = Command,
            };
        }
    }

    public sealed record DevSpaceTerminalChoice(string Name, string Value);

    public sealed class DevSpaceProfileSettings
    {
        public const string PowerShell7 = "__devspaces_pwsh__";
        public const string WindowsPowerShell = "__devspaces_powershell__";
        public const string CommandPrompt = "__devspaces_cmd__";
        public const string SystemShell = "__devspaces_shell__";

        public static DevSpaceProfileSettings Instance => _instance ??= Load();

        public string DefaultTerminal { get; set; }

        public List<DevSpaceTerminalProfile> Profiles { get; } = [];

        public static IReadOnlyList<DevSpaceTerminalChoice> SupportedTerminals
        {
            get
            {
                if (OperatingSystem.IsWindows())
                {
                    return
                    [
                        new DevSpaceTerminalChoice("PowerShell 7 (pwsh)", PowerShell7),
                        new DevSpaceTerminalChoice("Windows PowerShell", WindowsPowerShell),
                        new DevSpaceTerminalChoice("Command Prompt", CommandPrompt),
                    ];
                }

                return
                [
                    new DevSpaceTerminalChoice("System Shell", SystemShell),
                    new DevSpaceTerminalChoice("PowerShell 7 (pwsh)", PowerShell7),
                ];
            }
        }

        public static string GetTerminalDisplayName(string value)
        {
            return NormalizeTerminal(value) switch
            {
                PowerShell7 => "PowerShell 7",
                WindowsPowerShell => "Windows PowerShell",
                CommandPrompt => "Command Prompt",
                SystemShell => "Shell",
                _ => "Terminal",
            };
        }

        public static string ResolveWorkingDirectory(string workspacePath, string profilePath)
        {
            if (string.IsNullOrWhiteSpace(workspacePath))
                throw new ArgumentException("Workspace path must not be empty.", nameof(workspacePath));

            var workspace = Path.GetFullPath(workspacePath);
            var value = profilePath?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(value) || value == "." || value == "./" || value == ".\\")
                return workspace;

            if (value.StartsWith("//", StringComparison.Ordinal) ||
                value.StartsWith("\\\\", StringComparison.Ordinal) ||
                (value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':'))
            {
                throw new ArgumentException("Terminal profile paths must be relative to the workspace.", nameof(profilePath));
            }

            if (value.StartsWith("./", StringComparison.Ordinal) || value.StartsWith(".\\", StringComparison.Ordinal))
                value = value[2..];

            if (value.Length > 0 && (value[0] == '/' || value[0] == '\\'))
                value = value[1..];

            value = value.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            var resolved = Path.GetFullPath(Path.Combine(workspace, value));
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (string.Equals(resolved, workspace, comparison))
                return resolved;

            var prefix = workspace.EndsWith(Path.DirectorySeparatorChar)
                ? workspace
                : workspace + Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(prefix, comparison))
                throw new ArgumentException("Terminal profile path escapes the current workspace.", nameof(profilePath));

            return resolved;
        }

        public void Save()
        {
            try
            {
                var data = new DevSpaceSettingsData
                {
                    Version = 1,
                    DefaultTerminal = NormalizeTerminal(DefaultTerminal),
                    Profiles = CloneProfiles(Profiles),
                };
                var file = Path.Combine(Native.OS.DataDir, "devspaces.json");
                using var stream = File.Create(file);
                JsonSerializer.Serialize(stream, data, DevSpaceProfileJsonContext.Default.DevSpaceSettingsData);
            }
            catch
            {
                // Preferences persistence should never prevent SourceGit from running.
            }
        }

        public async Task ExportProfilesAsync(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            var data = new DevSpaceProfileExport
            {
                Version = 1,
                Profiles = CloneProfiles(Profiles),
            };
            await JsonSerializer.SerializeAsync(stream, data, DevSpaceProfileJsonContext.Default.DevSpaceProfileExport);
        }

        public async Task<int> ImportProfilesAsync(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            var data = await JsonSerializer.DeserializeAsync(stream, DevSpaceProfileJsonContext.Default.DevSpaceProfileExport);
            if (data == null || data.Version != 1)
                throw new InvalidDataException("Unsupported DevSpace terminal profile JSON version.");

            var imported = 0;
            foreach (var profile in data.Profiles ?? [])
            {
                ValidateProfile(profile);
                var index = Profiles.FindIndex(x => string.Equals(x.Id, profile.Id, StringComparison.OrdinalIgnoreCase));
                var copy = profile.Clone();
                if (index >= 0)
                    Profiles[index] = copy;
                else
                    Profiles.Add(copy);
                imported++;
            }

            Save();
            return imported;
        }

        public static void ValidateProfile(DevSpaceTerminalProfile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            if (string.IsNullOrWhiteSpace(profile.Name))
                throw new ArgumentException("Terminal profile name must not be empty.");
            if (string.IsNullOrWhiteSpace(profile.Id))
                profile.Id = Guid.NewGuid().ToString("D");
        }

        private DevSpaceProfileSettings(string defaultTerminal)
        {
            DefaultTerminal = NormalizeTerminal(defaultTerminal);
        }

        private static DevSpaceProfileSettings Load()
        {
            var fallback = NormalizeTerminal(ViewModels.Preferences.Instance.DevSpacesDefaultCommand);
            var settings = new DevSpaceProfileSettings(fallback);
            var file = Path.Combine(Native.OS.DataDir, "devspaces.json");
            if (!File.Exists(file))
                return settings;

            try
            {
                using var stream = File.OpenRead(file);
                var data = JsonSerializer.Deserialize(stream, DevSpaceProfileJsonContext.Default.DevSpaceSettingsData);
                if (data == null || data.Version != 1)
                    return settings;

                settings.DefaultTerminal = NormalizeTerminal(data.DefaultTerminal);
                foreach (var profile in data.Profiles ?? [])
                {
                    ValidateProfile(profile);
                    settings.Profiles.Add(profile.Clone());
                }
            }
            catch
            {
                // Fall back to legacy/default DevSpace settings when the file is invalid.
            }

            return settings;
        }

        private static string NormalizeTerminal(string value)
        {
            var normalized = value?.Trim().ToLowerInvariant();
            if (OperatingSystem.IsWindows())
            {
                return normalized switch
                {
                    "pwsh" or PowerShell7 => PowerShell7,
                    "powershell" or "powershell.exe" or WindowsPowerShell => WindowsPowerShell,
                    "cmd" or "cmd.exe" or CommandPrompt => CommandPrompt,
                    _ => PowerShell7,
                };
            }

            return normalized switch
            {
                "pwsh" or PowerShell7 => PowerShell7,
                _ => SystemShell,
            };
        }

        private static List<DevSpaceTerminalProfile> CloneProfiles(IEnumerable<DevSpaceTerminalProfile> profiles)
        {
            var result = new List<DevSpaceTerminalProfile>();
            foreach (var profile in profiles)
                result.Add(profile.Clone());
            return result;
        }

        private static DevSpaceProfileSettings _instance;
    }

    internal sealed class DevSpaceSettingsData
    {
        public int Version { get; set; } = 1;
        public string DefaultTerminal { get; set; } = DevSpaceProfileSettings.SystemShell;
        public List<DevSpaceTerminalProfile> Profiles { get; set; } = [];
    }

    internal sealed class DevSpaceProfileExport
    {
        public int Version { get; set; } = 1;
        public List<DevSpaceTerminalProfile> Profiles { get; set; } = [];
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(DevSpaceSettingsData))]
    [JsonSerializable(typeof(DevSpaceProfileExport))]
    internal partial class DevSpaceProfileJsonContext : JsonSerializerContext
    {
    }
}
