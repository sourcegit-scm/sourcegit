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
        public const string ARG_PLACEHOLDER = "$ARG";

        public string Name { get; private set; }
        public string Executable { get; private set; }
        public IEnumerable<string> OpenCmdArgs { get; private set; }
        public Bitmap IconImage { get; private set; } = null;

        public ExternalTool(string name, string icon, string executable, IEnumerable<string> openCmdArgs)
        {
            Name = name;
            Executable = executable;
            OpenCmdArgs = openCmdArgs;

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

        public void Open(string repo)
        {
            var args = new List<string>(OpenCmdArgs);
            int i = args.IndexOf(ARG_PLACEHOLDER);
            if (i != -1) {
                args[i] = repo;
            }

            Process.Start(new ProcessStartInfo(Executable, args)
            {
                WorkingDirectory = repo,
                UseShellExecute = false,
            });
        }
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

    public class ExternalToolPaths
    {
        [JsonPropertyName("tools")]
        public Dictionary<string, string> Tools { get; set; } = new Dictionary<string, string>();
    }

    public class ExternalToolsFinder
    {
        public List<ExternalTool> Founded
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
                    _customPaths = JsonSerializer.Deserialize(File.ReadAllText(customPathsConfig), JsonCodeGen.Default.ExternalToolPaths);
            }
            catch
            {
                // Ignore
            }

            if (_customPaths == null)
                _customPaths = new ExternalToolPaths();
        }

        public void TryAdd(string name, string icon, IEnumerable<string> args, string key, Func<string> finder)
        {
            if (_customPaths.Tools.TryGetValue(key, out var customPath) && File.Exists(customPath))
            {
                Founded.Add(new ExternalTool(name, icon, customPath, args));
            }
            else
            {
                var path = finder();
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    Founded.Add(new ExternalTool(name, icon, path, args));
            }
        }

        public void VSCode(Func<string> platformFinder)
        {
            TryAdd("Visual Studio Code", "vscode", [ExternalTool.ARG_PLACEHOLDER], "VSCODE", platformFinder);
        }

        public void VSCodeInsiders(Func<string> platformFinder)
        {
            TryAdd("Visual Studio Code - Insiders", "vscode_insiders", [ExternalTool.ARG_PLACEHOLDER], "VSCODE_INSIDERS", platformFinder);
        }

        public void VSCodium(Func<string> platformFinder)
        {
            TryAdd("VSCodium", "codium", [ExternalTool.ARG_PLACEHOLDER], "VSCODIUM", platformFinder);
        }

        public void Fleet(Func<string> platformFinder)
        {
            TryAdd("Fleet", "fleet", [ExternalTool.ARG_PLACEHOLDER], "FLEET", platformFinder);
        }

        public void SublimeText(Func<string> platformFinder)
        {
            TryAdd("Sublime Text", "sublime_text", [ExternalTool.ARG_PLACEHOLDER], "SUBLIME_TEXT", platformFinder);
        }

        public void FindJetBrainsFromToolbox(Func<string> platformFinder)
        {
            var exclude = new List<string> { "fleet", "dotmemory", "dottrace", "resharper-u", "androidstudio" };
            var supported_icons = new List<string> { "CL", "DB", "DL", "DS", "GO", "JB", "PC", "PS", "PY", "QA", "QD", "RD", "RM", "RR", "WRS", "WS" };
            var state = Path.Combine(platformFinder(), "state.json");
            if (File.Exists(state))
            {
                var stateData = JsonSerializer.Deserialize(File.ReadAllText(state), JsonCodeGen.Default.JetBrainsState);
                foreach (var tool in stateData.Tools)
                {
                    if (exclude.Contains(tool.ToolId.ToLowerInvariant()))
                        continue;

                    Founded.Add(new ExternalTool(
                        $"{tool.DisplayName} {tool.DisplayVersion}",
                        supported_icons.Contains(tool.ProductCode) ? $"JetBrains/{tool.ProductCode}" : "JetBrains/JB",
                        Path.Combine(tool.InstallLocation, tool.LaunchCommand),
                        [ExternalTool.ARG_PLACEHOLDER]));
                }
            }
        }

        private ExternalToolPaths _customPaths = null;
    }
}
