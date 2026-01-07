using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace SourceGit.Models
{
    public class ExternalTool
    {
        public string Name { get; }
        public string ExecFile { get; }
        public Bitmap IconImage { get; }

        public ExternalTool(string name, string icon, string execFile, Func<string, string> execArgsGenerator = null)
        {
            Name = name;
            ExecFile = execFile;
            _execArgsGenerator = execArgsGenerator ?? (path => path.Quoted());

            try
            {
                var asset = AssetLoader.Open(new Uri($"avares://SourceGit/Resources/Images/ExternalToolIcons/{icon}.png",
                    UriKind.RelativeOrAbsolute));
                IconImage = new Bitmap(asset);
            }
            catch
            {
                // ignore
            }
        }

        public void Open(string path)
        {
            // The executable file may be removed after the tool list is loaded (once time on startup).
            if (!File.Exists(ExecFile))
                return;

            Process.Start(new ProcessStartInfo()
            {
                FileName = ExecFile,
                Arguments = _execArgsGenerator.Invoke(path),
                UseShellExecute = false,
            });
        }

        private Func<string, string> _execArgsGenerator = null;
    }

    public class VisualStudioInstance
    {
        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("productPath")]
        public string ProductPath { get; set; } = string.Empty;

        [JsonPropertyName("isPrerelease")]
        public bool IsPrerelease { get; set; } = false;
    }

    public class JetBrainsState
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = 0;
        [JsonPropertyName("appVersion")]
        public string AppVersion { get; set; } = string.Empty;
        [JsonPropertyName("tools")]
        public List<JetBrainsTool> Tools { get; set; } = new List<JetBrainsTool>();
    }

    public class JetBrainsTool
    {
        [JsonPropertyName("channelId")]
        public string ChannelId { get; set; }
        [JsonPropertyName("toolId")]
        public string ToolId { get; set; }
        [JsonPropertyName("productCode")]
        public string ProductCode { get; set; }
        [JsonPropertyName("tag")]
        public string Tag { get; set; }
        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }
        [JsonPropertyName("displayVersion")]
        public string DisplayVersion { get; set; }
        [JsonPropertyName("buildNumber")]
        public string BuildNumber { get; set; }
        [JsonPropertyName("installLocation")]
        public string InstallLocation { get; set; }
        [JsonPropertyName("launchCommand")]
        public string LaunchCommand { get; set; }
    }

    public class ExternalToolCustomization
    {
        [JsonPropertyName("tools")]
        public Dictionary<string, string> Tools { get; set; } = new Dictionary<string, string>();
        [JsonPropertyName("excludes")]
        public List<string> Excludes { get; set; } = new List<string>();
    }

    public class ExternalToolsFinder
    {
        public List<ExternalTool> Tools
        {
            get;
            private set;
        } = new List<ExternalTool>();

        public ExternalToolsFinder()
        {
            var customPathsConfig = Path.Combine(Native.OS.DataDir, "external_editors.json");
            try
            {
                if (File.Exists(customPathsConfig))
                {
                    using var stream = File.OpenRead(customPathsConfig);
                    _customization = JsonSerializer.Deserialize(stream, JsonCodeGen.Default.ExternalToolCustomization);
                }
            }
            catch
            {
                // Ignore
            }

            _customization ??= new ExternalToolCustomization();
        }

        public void TryAdd(string name, string icon, Func<string> finder, Func<string, string> execArgsGenerator = null)
        {
            if (_customization.Excludes.Contains(name))
                return;

            if (_customization.Tools.TryGetValue(name, out var customPath) && File.Exists(customPath))
            {
                Tools.Add(new ExternalTool(name, icon, customPath, execArgsGenerator));
            }
            else
            {
                var path = finder();
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    Tools.Add(new ExternalTool(name, icon, path, execArgsGenerator));
            }
        }

        public void VSCode(Func<string> platformFinder)
        {
            TryAdd("Visual Studio Code", "vscode", platformFinder);
        }

        public void VSCodeInsiders(Func<string> platformFinder)
        {
            TryAdd("Visual Studio Code - Insiders", "vscode_insiders", platformFinder);
        }

        public void VSCodium(Func<string> platformFinder)
        {
            TryAdd("VSCodium", "codium", platformFinder);
        }

        public void SublimeText(Func<string> platformFinder)
        {
            TryAdd("Sublime Text", "sublime_text", platformFinder);
        }

        public void Zed(Func<string> platformFinder)
        {
            TryAdd("Zed", "zed", platformFinder);
        }

        public void Cursor(Func<string> platformFinder)
        {
            TryAdd("Cursor", "cursor", platformFinder);
        }

        public void FindJetBrainsFromToolbox(Func<string> platformFinder)
        {
            var exclude = new List<string> { "fleet", "dotmemory", "dottrace", "resharper-u", "androidstudio" };
            var supportedIcons = new List<string> { "CL", "DB", "DL", "DS", "GO", "JB", "PC", "PS", "PY", "QA", "QD", "RD", "RM", "RR", "WRS", "WS" };
            var state = Path.Combine(platformFinder(), "state.json");
            if (File.Exists(state))
            {
                try
                {
                    using var stream = File.OpenRead(state);
                    var stateData = JsonSerializer.Deserialize(stream, JsonCodeGen.Default.JetBrainsState);
                    foreach (var tool in stateData.Tools)
                    {
                        if (exclude.Contains(tool.ToolId.ToLowerInvariant()))
                            continue;

                        Tools.Add(new ExternalTool(
                            $"{tool.DisplayName} {tool.DisplayVersion}",
                            supportedIcons.Contains(tool.ProductCode) ? $"JetBrains/{tool.ProductCode}" : "JetBrains/JB",
                            Path.Combine(tool.InstallLocation, tool.LaunchCommand)));
                    }
                }
                catch
                {
                    // Ignore exceptions.
                }
            }
        }

        private ExternalToolCustomization _customization = null;
    }
}
