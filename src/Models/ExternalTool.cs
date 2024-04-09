using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

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
                var icon = AssetLoader.Open(new Uri($"avares://SourceGit/Resources/ExternalToolIcons/{Icon}.png", UriKind.RelativeOrAbsolute));
                return new Bitmap(icon);
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
                Executable = path,
            });
        }
    }
}
