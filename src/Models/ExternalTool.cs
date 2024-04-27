using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace SourceGit.Models
{
    public class ExternalTool
    {
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Executable { get; set; } = string.Empty;
        public string OpenCmdArgs { get; set; } = string.Empty;

        public Bitmap IconImage
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Icon))
                {
                    return null;
                }

                try
                {
                    var icon = AssetLoader.Open(new Uri($"avares://SourceGit/Resources/ExternalToolIcons/{Icon}.png", UriKind.RelativeOrAbsolute));
                    return new Bitmap(icon);
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        public void Open(string repo)
        {
            Process.Start(new ProcessStartInfo()
            {
                WorkingDirectory = repo,
                FileName = Executable,
                Arguments = string.Format(OpenCmdArgs, repo),
                UseShellExecute = false,
            });
        }
    }

    public class JetBrainsState
    {
        public int Version { get; set; }
        public string AppVersion { get; set; }
        public List<JetBrainsTool> Tools { get; set; }
    }

    public class JetBrainsTool
    {
        public string ChannelId { get; set; }
        public string ToolId { get; set; }
        public string ProductCode { get; set; }
        public string Tag { get; set; }
        public string DisplayName { get; set; }
        public string DisplayVersion { get; set; }
        public string BuildNumber { get; set; }
        public string InstallLocation { get; set; }
        public string LaunchCommand { get; set; }
    }

    public class ExternalToolsFinder
    {
        public List<ExternalTool> Founded
        {
            get;
            private set;
        } = new List<ExternalTool>();

        public void TryAdd(string name, string icon, string args, string env, Func<string> finder)
        {
            var path = Environment.GetEnvironmentVariable(env);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                path = finder();
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    return;
            }

            Founded.Add(new ExternalTool
            {
                Name = name,
                Icon = icon,
                OpenCmdArgs = args,
                Executable = path
            });
        }

        public void VSCode(Func<string> platform_finder)
        {
            TryAdd("Visual Studio Code", "vscode", "\"{0}\"", "VSCODE_PATH", platform_finder);
        }

        public void VSCodeInsiders(Func<string> platform_finder)
        {
            TryAdd("Visual Studio Code - Insiders", "vscode_insiders", "\"{0}\"", "VSCODE_INSIDERS_PATH", platform_finder);
        }

        public void Fleet(Func<string> platform_finder)
        {
            TryAdd("JetBrains Fleet", "fleet", "\"{0}\"", "FLEET_PATH", platform_finder);
        }

        public void SublimeText(Func<string> platform_finder)
        {
            TryAdd("Sublime Text", "sublime_text", "\"{0}\"", "SUBLIME_TEXT_PATH", platform_finder);
        }

        public void FindJetBrainsFromToolbox(Func<string> platform_finder)
        {
            var exclude = new List<string> { "fleet", "dotmemory", "dottrace", "resharper-u", "androidstudio" };
            var supported_icons = new List<string> { "CL", "DB", "DL", "DS", "GO", "IC", "IU", "JB", "PC", "PS", "PY", "QA", "QD", "RD", "RM", "RR", "WRS", "WS" };
            var state = Path.Combine(platform_finder(), "state.json");
            if (File.Exists(state))
            {
                var stateData = JsonSerializer.Deserialize(File.ReadAllText(state), JsonCodeGen.Default.JetBrainsState);
                foreach (var tool in stateData.Tools)
                {
                    if (exclude.Contains(tool.ToolId.ToLowerInvariant()))
                        continue;

                    Founded.Add(new ExternalTool
                    {
                        Name = $"JetBrains {tool.DisplayName} {tool.DisplayVersion}",
                        Icon = supported_icons.Contains(tool.ProductCode) ? $"JetBrains/{tool.ProductCode}" : $"JetBrains/JB",
                        OpenCmdArgs = "\"{0}\"",
                        Executable = Path.Combine(tool.InstallLocation, tool.LaunchCommand),
                    });
                }
            }
        }
    }
}
