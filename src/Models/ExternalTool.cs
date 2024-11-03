using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace SourceGit.Models
{
    public class ExternalTool
    {
        public string Name { get; private set; }
        public string Executable { get; private set; }
        public string OpenCmdArgs { get; private set; }
        public Bitmap IconImage { get; private set; } = null;
        public Func<string, string> ArgTransform { get; private set; }

        public ExternalTool(string name, string icon, string executable, string openCmdArgs, Func<string, string> argsTransform)
        {
            Name = name;
            Executable = executable;
            OpenCmdArgs = openCmdArgs;
            ArgTransform = argsTransform ?? ((s) => s);

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
            string arguments = string.Format(OpenCmdArgs, repo);

            if (ArgTransform != null)
                arguments = ArgTransform.Invoke(arguments);

            Process.Start(new ProcessStartInfo()
            {
                WorkingDirectory = repo,
                FileName = Executable,
                Arguments = arguments,
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

        public void TryAdd(string name, string icon, string args, string key, Func<string> finder, Func<string, string> argsTransform = null)
        {
            if (_customPaths.Tools.TryGetValue(key, out var customPath) && File.Exists(customPath))
            {
                Founded.Add(new ExternalTool(name, icon, customPath, args, argsTransform));
            }
            else
            {
                var path = finder();
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    Founded.Add(new ExternalTool(name, icon, path, args, argsTransform));
            }
        }

        public void VSCode(Func<string> platformFinder)
        {
            TryAdd("Visual Studio Code", "vscode", "\"{0}\"", "VSCODE", platformFinder);
        }

        public void VSCodeInsiders(Func<string> platformFinder)
        {
            TryAdd("Visual Studio Code - Insiders", "vscode_insiders", "\"{0}\"", "VSCODE_INSIDERS", platformFinder);
        }

        public void VSCodium(Func<string> platformFinder)
        {
            TryAdd("VSCodium", "codium", "\"{0}\"", "VSCODIUM", platformFinder);
        }

        public void Fleet(Func<string> platformFinder)
        {
            TryAdd("Fleet", "fleet", "\"{0}\"", "FLEET", platformFinder);
        }

        public void SublimeText(Func<string> platformFinder)
        {
            TryAdd("Sublime Text", "sublime_text", "\"{0}\"", "SUBLIME_TEXT", platformFinder);
        }

        public void Zed(Func<string> platformFinder)
        {
            TryAdd("Zed", "zed", "\"{0}\"", "ZED", platformFinder);
        }

        public void VisualStudio(Func<string> platformFinder)
        {
            TryAdd("Visual Studio", "vs", "\"{0}\"", "VISUALSTUDIO", platformFinder, VisualStudioTryFindSolution);
        }

        private static string VisualStudioTryFindSolution(string path)
        {
            try
            {
                if (Directory.GetFiles(path.Trim('\"'), "*.sln", SearchOption.AllDirectories).FirstOrDefault() is string solutionPath)
                    return Path.GetFullPath(solutionPath);
            }
            catch
            {
                // do nothing
            }
            return path;
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
                        "\"{0}\"",
                        null));
                }
            }
        }

        private ExternalToolPaths _customPaths = null;
    }
}
