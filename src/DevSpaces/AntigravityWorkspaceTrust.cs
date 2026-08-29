using System;
using System.IO;
using System.Text.Json;

namespace SourceGit.DevSpaces
{
    public static class AntigravityWorkspaceTrust
    {
        public static bool EnsureTrusted(string workspacePath)
        {
            var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(userHome))
                return false;

            var antigravityHome = Path.Combine(userHome, ".gemini", "antigravity-cli");
            return EnsureTrusted(workspacePath, antigravityHome);
        }

        public static bool EnsureTrusted(string workspacePath, string antigravityHome)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(workspacePath) || string.IsNullOrWhiteSpace(antigravityHome))
                    return false;

                var workspace = NormalizePath(workspacePath);
                var home = Path.GetFullPath(antigravityHome);
                Directory.CreateDirectory(home);

                var settingsPath = Path.Combine(home, "settings.json");
                if (!File.Exists(settingsPath))
                {
                    WriteNewSettings(settingsPath, workspace);
                    return true;
                }

                using var settings = JsonDocument.Parse(File.ReadAllBytes(settingsPath));
                if (settings.RootElement.ValueKind != JsonValueKind.Object)
                    return false;

                if (settings.RootElement.TryGetProperty("trustedWorkspaces", out var trustedWorkspaces))
                {
                    if (trustedWorkspaces.ValueKind != JsonValueKind.Array)
                        return false;

                    foreach (var trustedWorkspace in trustedWorkspaces.EnumerateArray())
                    {
                        if (trustedWorkspace.ValueKind != JsonValueKind.String)
                            return false;

                        if (PathsEqual(workspace, trustedWorkspace.GetString()))
                            return true;
                    }
                }

                WriteUpdatedSettings(settingsPath, settings.RootElement, workspace);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (JsonException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static bool PathsEqual(string workspace, string trustedWorkspace)
        {
            if (string.IsNullOrWhiteSpace(trustedWorkspace))
                return false;

            string trusted;
            try
            {
                trusted = NormalizePath(trustedWorkspace);
            }
            catch (ArgumentException)
            {
                return false;
            }

            return string.Equals(
                workspace,
                trusted,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        private static string NormalizePath(string path)
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }

        private static void WriteNewSettings(string settingsPath, string workspace)
        {
            WriteAtomically(settingsPath, writer =>
            {
                writer.WriteStartObject();
                writer.WritePropertyName("trustedWorkspaces");
                writer.WriteStartArray();
                writer.WriteStringValue(workspace);
                writer.WriteEndArray();
                writer.WriteEndObject();
            });
        }

        private static void WriteUpdatedSettings(string settingsPath, JsonElement root, string workspace)
        {
            WriteAtomically(settingsPath, writer =>
            {
                writer.WriteStartObject();
                var foundTrustedWorkspaces = false;

                foreach (var property in root.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    if (property.NameEquals("trustedWorkspaces"))
                    {
                        foundTrustedWorkspaces = true;
                        writer.WriteStartArray();
                        foreach (var trustedWorkspace in property.Value.EnumerateArray())
                            trustedWorkspace.WriteTo(writer);
                        writer.WriteStringValue(workspace);
                        writer.WriteEndArray();
                    }
                    else
                    {
                        property.Value.WriteTo(writer);
                    }
                }

                if (!foundTrustedWorkspaces)
                {
                    writer.WritePropertyName("trustedWorkspaces");
                    writer.WriteStartArray();
                    writer.WriteStringValue(workspace);
                    writer.WriteEndArray();
                }

                writer.WriteEndObject();
            });
        }

        private static void WriteAtomically(string settingsPath, Action<Utf8JsonWriter> write)
        {
            var directory = Path.GetDirectoryName(settingsPath);
            if (string.IsNullOrWhiteSpace(directory))
                throw new IOException("Antigravity configuration directory could not be resolved.");

            var tempPath = Path.Combine(directory, $".{Path.GetFileName(settingsPath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    write(writer);
                    writer.Flush();
                    stream.Flush(true);
                }

                File.Move(tempPath, settingsPath, true);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
    }
}
