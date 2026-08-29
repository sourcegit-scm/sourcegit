using System;
using System.IO;
using System.Text.Json;

namespace SourceGit.DevSpaces
{
    public static class CopilotWorkspaceTrust
    {
        public static bool EnsureTrusted(string workspacePath)
        {
            var copilotHome = Environment.GetEnvironmentVariable("COPILOT_HOME");
            if (string.IsNullOrWhiteSpace(copilotHome))
            {
                var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrWhiteSpace(userHome))
                    return false;

                copilotHome = Path.Combine(userHome, ".copilot");
            }

            return EnsureTrusted(workspacePath, copilotHome);
        }

        public static bool EnsureTrusted(string workspacePath, string copilotHome)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(workspacePath) || string.IsNullOrWhiteSpace(copilotHome))
                    return false;

                var workspace = NormalizePath(workspacePath);
                var home = Path.GetFullPath(copilotHome);
                Directory.CreateDirectory(home);

                var configPath = Path.Combine(home, "config.json");
                if (!File.Exists(configPath))
                {
                    WriteNewConfig(configPath, workspace);
                    return true;
                }

                using var config = JsonDocument.Parse(File.ReadAllBytes(configPath));
                if (config.RootElement.ValueKind != JsonValueKind.Object)
                    return false;

                if (config.RootElement.TryGetProperty("trustedFolders", out var trustedFolders))
                {
                    if (trustedFolders.ValueKind != JsonValueKind.Array)
                        return false;

                    foreach (var trustedFolder in trustedFolders.EnumerateArray())
                    {
                        if (trustedFolder.ValueKind != JsonValueKind.String)
                            return false;

                        if (IsTrustedBy(workspace, trustedFolder.GetString()))
                            return true;
                    }
                }

                WriteUpdatedConfig(configPath, config.RootElement, workspace);
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

        private static bool IsTrustedBy(string workspace, string trustedFolder)
        {
            if (string.IsNullOrWhiteSpace(trustedFolder))
                return false;

            string trusted;
            try
            {
                trusted = NormalizePath(trustedFolder);
            }
            catch (ArgumentException)
            {
                return false;
            }

            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (string.Equals(workspace, trusted, comparison))
                return true;

            var prefix = Path.EndsInDirectorySeparator(trusted)
                ? trusted
                : trusted + Path.DirectorySeparatorChar;
            return workspace.StartsWith(prefix, comparison);
        }

        private static string NormalizePath(string path)
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }

        private static void WriteNewConfig(string configPath, string workspace)
        {
            WriteAtomically(configPath, writer =>
            {
                writer.WriteStartObject();
                writer.WritePropertyName("trustedFolders");
                writer.WriteStartArray();
                writer.WriteStringValue(workspace);
                writer.WriteEndArray();
                writer.WriteEndObject();
            });
        }

        private static void WriteUpdatedConfig(string configPath, JsonElement root, string workspace)
        {
            WriteAtomically(configPath, writer =>
            {
                writer.WriteStartObject();
                var foundTrustedFolders = false;

                foreach (var property in root.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    if (property.NameEquals("trustedFolders"))
                    {
                        foundTrustedFolders = true;
                        writer.WriteStartArray();
                        foreach (var trustedFolder in property.Value.EnumerateArray())
                            trustedFolder.WriteTo(writer);
                        writer.WriteStringValue(workspace);
                        writer.WriteEndArray();
                    }
                    else
                    {
                        property.Value.WriteTo(writer);
                    }
                }

                if (!foundTrustedFolders)
                {
                    writer.WritePropertyName("trustedFolders");
                    writer.WriteStartArray();
                    writer.WriteStringValue(workspace);
                    writer.WriteEndArray();
                }

                writer.WriteEndObject();
            });
        }

        private static void WriteAtomically(string configPath, Action<Utf8JsonWriter> write)
        {
            var directory = Path.GetDirectoryName(configPath);
            if (string.IsNullOrWhiteSpace(directory))
                throw new IOException("Copilot configuration directory could not be resolved.");

            var tempPath = Path.Combine(directory, $".{Path.GetFileName(configPath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    write(writer);
                    writer.Flush();
                    stream.Flush(true);
                }

                File.Move(tempPath, configPath, true);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
    }
}
