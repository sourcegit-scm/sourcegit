using System;
using System.Collections.Generic;
using System.IO;

using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace SourceGit.Models
{
    public class ExternalMerger
    {
        public int Type { get; set; }
        public string Icon { get; set; }
        public string Name { get; set; }
        public string Exec { get; set; }
        public string Cmd { get; set; }
        public string DiffCmd { get; set; }

        public Bitmap IconImage
        {
            get
            {
                var icon = AssetLoader.Open(new Uri($"avares://SourceGit/Resources/Images/ExternalToolIcons/{Icon}.png", UriKind.RelativeOrAbsolute));
                return new Bitmap(icon);
            }
        }

        public static readonly List<ExternalMerger> Supported;

        static ExternalMerger()
        {
            if (OperatingSystem.IsWindows())
            {
                Supported = new List<ExternalMerger>() {
                    new ExternalMerger(0, "git", "Use Git Settings", "", "", ""),
                    new ExternalMerger(1, "vscode", "Visual Studio Code", "Code.exe", "-n --wait \"$MERGED\"", "-n --wait --diff \"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(2, "vscode_insiders", "Visual Studio Code - Insiders", "Code - Insiders.exe", "-n --wait \"$MERGED\"", "-n --wait --diff \"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(3, "vs", "Visual Studio", "vsDiffMerge.exe", "\"$REMOTE\" \"$LOCAL\" \"$BASE\" \"$MERGED\" /m", "\"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(4, "tortoise_merge", "Tortoise Merge", "TortoiseMerge.exe;TortoiseGitMerge.exe", "-base:\"$BASE\" -theirs:\"$REMOTE\" -mine:\"$LOCAL\" -merged:\"$MERGED\"", "-base:\"$LOCAL\" -theirs:\"$REMOTE\""),
                    new ExternalMerger(5, "kdiff3", "KDiff3", "kdiff3.exe", "\"$REMOTE\" -b \"$BASE\" \"$LOCAL\" -o \"$MERGED\"", "\"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(6, "beyond_compare", "Beyond Compare", "BComp.exe", "\"$REMOTE\" \"$LOCAL\" \"$BASE\" \"$MERGED\"", "\"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(7, "win_merge", "WinMerge", "WinMergeU.exe", "\"$MERGED\"", "-u -e -sw \"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(8, "codium", "VSCodium", "VSCodium.exe", "-n --wait \"$MERGED\"", "-n --wait --diff \"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(9, "p4merge", "P4Merge", "p4merge.exe", "-tw 4 \"$BASE\" \"$LOCAL\" \"$REMOTE\" \"$MERGED\"", "-tw 4 \"$LOCAL\" \"$REMOTE\""),
                };
            }
            else if (OperatingSystem.IsMacOS())
            {
                Supported = new List<ExternalMerger>() {
                    new ExternalMerger(0, "git", "Use Git Settings", "", "", ""),
                    new ExternalMerger(1, "xcode", "FileMerge", "/usr/bin/opendiff", "\"$BASE\" \"$LOCAL\" \"$REMOTE\" -ancestor \"$MERGED\"", "\"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(2, "vscode", "Visual Studio Code", "/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code", "-n --wait \"$MERGED\"", "-n --wait --diff \"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(3, "vscode_insiders", "Visual Studio Code - Insiders", "/Applications/Visual Studio Code - Insiders.app/Contents/Resources/app/bin/code", "-n --wait \"$MERGED\"", "-n --wait --diff \"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(4, "kdiff3", "KDiff3", "/Applications/kdiff3.app/Contents/MacOS/kdiff3", "\"$REMOTE\" -b \"$BASE\" \"$LOCAL\" -o \"$MERGED\"", "\"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(5, "beyond_compare", "Beyond Compare", "/Applications/Beyond Compare.app/Contents/MacOS/bcomp", "\"$REMOTE\" \"$LOCAL\" \"$BASE\" \"$MERGED\"", "\"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(6, "codium", "VSCodium", "/Applications/VSCodium.app/Contents/Resources/app/bin/codium", "-n --wait \"$MERGED\"", "-n --wait --diff \"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(7, "p4merge", "P4Merge", "/Applications/p4merge.app/Contents/Resources/launchp4merge", "-tw 4 \"$BASE\" \"$LOCAL\" \"$REMOTE\" \"$MERGED\"", "-tw 4 \"$LOCAL\" \"$REMOTE\""),
                };
            }
            else if (OperatingSystem.IsLinux())
            {
                Supported = new List<ExternalMerger>() {
                    new ExternalMerger(0, "git", "Use Git Settings", "", "", ""),
                    new ExternalMerger(1, "vscode", "Visual Studio Code", "/usr/share/code/code", "-n --wait \"$MERGED\"", "-n --wait --diff \"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(2, "vscode_insiders", "Visual Studio Code - Insiders", "/usr/share/code-insiders/code-insiders", "-n --wait \"$MERGED\"", "-n --wait --diff \"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(3, "kdiff3", "KDiff3", "/usr/bin/kdiff3", "\"$REMOTE\" -b \"$BASE\" \"$LOCAL\" -o \"$MERGED\"", "\"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(4, "beyond_compare", "Beyond Compare", "/usr/bin/bcomp", "\"$REMOTE\" \"$LOCAL\" \"$BASE\" \"$MERGED\"", "\"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(5, "meld", "Meld", "/usr/bin/meld", "\"$LOCAL\" \"$BASE\" \"$REMOTE\" --output \"$MERGED\"", "\"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(6, "codium", "VSCodium", "/usr/share/codium/bin/codium", "-n --wait \"$MERGED\"", "-n --wait --diff \"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(7, "p4merge", "P4Merge", "/usr/local/bin/p4merge", "-tw 4 \"$BASE\" \"$LOCAL\" \"$REMOTE\" \"$MERGED\"", "-tw 4 \"$LOCAL\" \"$REMOTE\""),
                };
            }
            else
            {
                Supported = new List<ExternalMerger>() {
                    new ExternalMerger(0, "git", "Use Git Settings", "", "", ""),
                };
            }
        }

        public ExternalMerger(int type, string icon, string name, string exec, string cmd, string diffCmd)
        {
            Type = type;
            Icon = icon;
            Name = name;
            Exec = exec;
            Cmd = cmd;
            DiffCmd = diffCmd;
        }

        public string[] GetPatterns()
        {
            if (OperatingSystem.IsWindows())
            {
                return Exec.Split(';');
            }
            else
            {
                var patterns = new List<string>();
                var choices = Exec.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var c in choices)
                {
                    patterns.Add(Path.GetFileName(c));
                }
                return patterns.ToArray();
            }
        }
    }
}
