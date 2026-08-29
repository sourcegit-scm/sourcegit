using System;
using System.IO;
using System.Text;

namespace DevBoard.DevSpaces
{
    public static class CodexWorkspaceTrust
    {
        public static bool EnsureTrusted(string workspacePath)
        {
            var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
            if (string.IsNullOrWhiteSpace(codexHome))
            {
                var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrWhiteSpace(userHome))
                    return false;

                codexHome = Path.Combine(userHome, ".codex");
            }

            return EnsureTrusted(workspacePath, codexHome);
        }

        public static bool EnsureTrusted(string workspacePath, string codexHome)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(workspacePath) || string.IsNullOrWhiteSpace(codexHome))
                    return false;

                var workspace = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspacePath));
                var home = Path.GetFullPath(codexHome);
                Directory.CreateDirectory(home);

                var configPath = Path.Combine(home, "config.toml");
                var header = $"[projects.\"{EscapeTomlBasicString(workspace)}\"]";
                var config = File.Exists(configPath) ? File.ReadAllText(configPath) : string.Empty;

                if (TryUpdateExistingProject(config, header, out var updated))
                {
                    if (!string.Equals(config, updated, StringComparison.Ordinal))
                        WriteAtomically(configPath, updated);
                    return true;
                }

                var builder = new StringBuilder(config);
                if (builder.Length > 0 && builder[^1] != '\n')
                    builder.AppendLine();
                if (builder.Length > 0)
                    builder.AppendLine();
                builder.AppendLine(header);
                builder.AppendLine("trust_level = \"trusted\"");
                WriteAtomically(configPath, builder.ToString());
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
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static bool TryUpdateExistingProject(string config, string header, out string updated)
        {
            updated = config;
            var headerIndex = config.IndexOf(header, StringComparison.Ordinal);
            if (headerIndex < 0)
                return false;

            var sectionStart = headerIndex + header.Length;
            var nextSection = config.IndexOf("\n[", sectionStart, StringComparison.Ordinal);
            var sectionEnd = nextSection >= 0 ? nextSection : config.Length;
            var section = config[sectionStart..sectionEnd];
            const string trusted = "trust_level = \"trusted\"";
            if (section.Contains(trusted, StringComparison.Ordinal))
                return true;

            var trustIndex = section.IndexOf("trust_level", StringComparison.Ordinal);
            if (trustIndex >= 0)
            {
                var absoluteTrustIndex = sectionStart + trustIndex;
                var lineEnd = config.IndexOf('\n', absoluteTrustIndex);
                if (lineEnd < 0 || lineEnd > sectionEnd)
                    lineEnd = sectionEnd;

                updated = string.Concat(config.AsSpan(0, absoluteTrustIndex), trusted, config.AsSpan(lineEnd));
                return true;
            }

            var insertion = sectionStart;
            updated = config.Insert(insertion, $"{Environment.NewLine}{trusted}");
            return true;
        }

        private static string EscapeTomlBasicString(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void WriteAtomically(string configPath, string content)
        {
            var directory = Path.GetDirectoryName(configPath);
            if (string.IsNullOrWhiteSpace(directory))
                throw new IOException("Codex configuration directory could not be resolved.");

            var tempPath = Path.Combine(directory, $".{Path.GetFileName(configPath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(tempPath, content);
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
