using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SourceGit.Native;

namespace SourceGit.Models
{
    public class ExternalTool
    {
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string FallbackIcon { get; set; } = string.Empty;
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

                if (File.Exists(Icon))
                {
                    return new Bitmap(Icon);
                }

                try
                {
                    var icon = AssetLoader.Open(new Uri($"avares://SourceGit/Resources/ExternalToolIcons/{Icon}.png", UriKind.RelativeOrAbsolute));
                    return new Bitmap(icon);
                }
                catch (Exception)
                {
                    if (!string.IsNullOrWhiteSpace(FallbackIcon))
                    {
                        var icon = AssetLoader.Open(new Uri($"avares://SourceGit/Resources/ExternalToolIcons/{FallbackIcon}.png", UriKind.RelativeOrAbsolute));
                        return new Bitmap(icon);
                    }

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

    public class JetBrainsTool
    {
        public string Name { get; set; }
        public string Instance { get; set; }
        public string Path { get; set; }
        public string Version { get; set; }
        public string BuildNumber { get; set; }
        public string ProductCode { get; set; }
        public string DataDirectoryName { get; set; }
        public string SvgIconPath { get; set; }
        public string PngIconPath => System.IO.Path.ChangeExtension(SvgIconPath, "png");
        public string IcoIconPath => System.IO.Path.ChangeExtension(SvgIconPath, "ico");
        public string ProductVendor { get; set; }
        public string Executable { get; set; }
        public string Icon { get; set; }
        public string FallbackIcon { get; set; }

        public override string ToString()
        {
            return $"{ProductVendor} {Name} {Version}";
        }
    }

    public class ExternalToolsFinder
    {
        public List<ExternalTool> Founded
        {
            get;
            private set;
        } = new List<ExternalTool>();

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

        public void TryAdd(string name, string icon, string args, string env, Func<string> finder, string fallbackIcon = "")
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
                Name = name, Icon = icon, OpenCmdArgs = args, Executable = path, FallbackIcon = fallbackIcon
            });
        }

        public void FindJetBrainsFromToolbox(Func<string> platform_finder)
        {
            var exclude = new[] { "fleet", "dotmemory", "dottrace", "resharper-u", "androidstudio" };
            var state = Path.Combine(platform_finder.Invoke(), "state.json");
            var models = Array.Empty<JetBrainsTool>();
            if (File.Exists(state))
            {
                var stateData = JsonSerializer.Deserialize<JetBrainsState>(File.ReadAllText(state), new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, });

                var tools = stateData.Tools
                    .Where(p => !exclude.Contains(p.ToolId.ToLowerInvariant()))
                    .ToArray();

                models = tools.Select(s =>
                {
                   return new JetBrainsTool()
                    {
                        Name = s.DisplayName,
                        Executable = s.LaunchCommand,
                        Icon =$"JetBrains/{s.ProductCode}",
                        FallbackIcon = $"JetBrains/JB",
                        Path = s.InstallLocation,
                        Version = s.DisplayVersion,
                        BuildNumber = s.BuildNumber,
                        ProductCode = s.ProductCode,
                        ProductVendor = "JetBrains",
                    };
                }).ToArray();
            }

            foreach (var model in models)
            {
                var item = new Func<string>(() =>
                {
                    return Path.Combine(model.Path, model.Executable);
                });
                var name = model.ProductVendor + "_" + model.ProductCode + (model.Instance != null ? $"_{model.Instance}" : string.Empty);
                TryAdd($"{model}", model.Icon, "\"{0}\"", $"{name.ToUpperInvariant()}_PATH", item, model.FallbackIcon);
            }
        }
        
        
        internal class JetBrainsState
        {
            public int Version { get; set; }
            public string AppVersion { get; set; }
            public List<Tool> Tools { get; set; }
        }

        internal class Tool
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
    }
}
