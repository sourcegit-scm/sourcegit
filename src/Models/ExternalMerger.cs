using System;
using System.Collections.Generic;
using System.IO;

using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace SourceGit.Models
{
    public class ExternalMerger(string icon, string name, string finder, string mergeCmd, string diffCmd)
    {
        public string Icon { get; } = icon;
        public string Name { get; } = name;
        public string Finder { get; } = finder;
        public string MergeCmd { get; } = mergeCmd;
        public string DiffCmd { get; } = diffCmd;

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
                    new ExternalMerger("git", "Use Git Settings", "", "", ""),
                    new ExternalMerger("vscode", "Visual Studio Code", "Code.exe", "-n --wait \"$MERGED\"", "-n --wait --diff \"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger("vscode_insiders", "Visual Studio Code - Insiders", "Code - Insiders.exe", "-n --wait \"$MERGED\"", "-n --wait --diff \"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger("vs", "Visual Studio", "vsDiffMerge.exe", "\"$REMOTE\" \"$LOCAL\" \"$BASE\" \"$MERGED\" /m", "\"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger("tortoise_merge", "Tortoise Merge", "TortoiseMerge.exe;TortoiseGitMerge.exe", "-base:\"$BASE\" -theirs:\"$REMOTE\" -mine:\"$LOCAL\" -merged:\"$MERGED\"", "-base:\"$LOCAL\" -theirs:\"$REMOTE\""),
                    new ExternalMerger("kdiff3", "KDiff3", "kdiff3.exe", "\"$REMOTE\" -b \"$BASE\" \"$LOCAL\" -o \"$MERGED\"", "\"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger("beyond_compare", "Beyond Compare", "BComp.exe", "\"$REMOTE\" \"$LOCAL\" \"$BASE\" \"$MERGED\"", "\"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger("win_merge", "WinMerge", "WinMergeU.exe", "\"$MERGED\"", "-u -e -sw \"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger("codium", "VSCodium", "VSCodium.exe", "-n --wait \"$MERGED\"", "-n --wait --diff \"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger("p4merge", "P4Merge", "p4merge.exe", "-tw 4 \"$BASE\" \"$LOCAL\" \"$REMOTE\" \"$MERGED\"", "-tw 4 \"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger("plastic_merge", "Plastic SCM", "mergetool.exe", "-s=\"$REMOTE\" -b=\"$BASE\" -d=\"$LOCAL\" -r=\"$MERGED\" --automatic", "-s=\"$LOCAL\" -d=\"$REMOTE\""),
                    new ExternalMerger("meld", "Meld", "Meld.exe", "\"$LOCAL\" \"$BASE\" \"$REMOTE\" --output \"$MERGED\"", "\"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger("cursor", "Cursor", "Cursor.exe", "-n --wait \"$MERGED\"", "-n --wait --diff \"$LOCAL\" \"$REMOTE\""),
                };
            }
            else if (OperatingSystem.IsMacOS())
            {
                Supported = new List<ExternalMerger>() {
                    new ExternalMerger("git", "Use Git Settings", "", "", ""),
                    new ExternalMerger("xcode", "FileMerge", "/usr/bin/opendiff", "\"$BASE\" \"$LOCAL\" \"$REMOTE\" -ancestor \"$MERGED\"", "\"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger("vscode", "Visual Studio Code", "/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code", "-n --wait \"$MERGED\"", "-n --wait --diff \"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger("vscode_insiders", "Visual Studio Code - Insiders", "/Applications/Visual Studio Code - Insiders.app/Contents/Resources/app/bin/code", "-n --wait \"$MERGED\"", "-n --wait --diff \"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger("kdiff3", "KDiff3", "/Applications/kdiff3.app/Contents/MacOS/kdiff3", "\"$REMOTE\" -b \"$BASE\" \"$LOCAL\" -o \"$MERGED\"", "\"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger("beyond_compare", "Beyond Compare", "/Applications/Beyond Compare.app/Contents/MacOS/bcomp", "\"$REMOTE\" \"$LOCAL\" \"$BASE\" \"$MERGED\"", "\"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger("codium", "VSCodium", "/Applications/VSCodium.app/Contents/Resources/app/bin/codium", "-n --wait \"$MERGED\"", "-n --wait --diff \"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger("p4merge", "P4Merge", "/Applications/p4merge.app/Contents/Resources/launchp4merge", "-tw 4 \"$BASE\" \"$LOCAL\" \"$REMOTE\" \"$MERGED\"", "-tw 4 \"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger("cursor", "Cursor", "/Applications/Cursor.app/Contents/Resources/app/bin/cursor", "-n --wait \"$MERGED\"", "-n --wait --diff \"$LOCAL\" \"$REMOTE\""),
                };
            }
            else if (OperatingSystem.IsLinux())
            {
                Supported = new List<ExternalMerger>() {
                    new ExternalMerger("git", "Use Git Settings", "", "", ""),
                    new ExternalMerger("vscode", "Visual Studio Code", "/usr/share/code/code", "-n --wait \"$MERGED\"", "-n --wait --diff \"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger("vscode_insiders", "Visual Studio Code - Insiders", "/usr/share/code-insiders/code-insiders", "-n --wait \"$MERGED\"", "-n --wait --diff \"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger("kdiff3", "KDiff3", "/usr/bin/kdiff3", "\"$REMOTE\" -b \"$BASE\" \"$LOCAL\" -o \"$MERGED\"", "\"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger("beyond_compare", "Beyond Compare", "/usr/bin/bcomp", "\"$REMOTE\" \"$LOCAL\" \"$BASE\" \"$MERGED\"", "\"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger("meld", "Meld", "/usr/bin/meld", "\"$LOCAL\" \"$BASE\" \"$REMOTE\" --output \"$MERGED\"", "\"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger("codium", "VSCodium", "/usr/share/codium/bin/codium", "-n --wait \"$MERGED\"", "-n --wait --diff \"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger("p4merge", "P4Merge", "/usr/local/bin/p4merge", "-tw 4 \"$BASE\" \"$LOCAL\" \"$REMOTE\" \"$MERGED\"", "-tw 4 \"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger("cursor", "Cursor", "cursor", "-n --wait \"$MERGED\"", "-n --wait --diff \"$LOCAL\" \"$REMOTE\""),
                };
            }
            else
            {
                Supported = new List<ExternalMerger>() {
                    new ExternalMerger("git", "Use Git Settings", "", "", ""),
                };
            }
        }

        public string[] GetPatternsToFindExecFile()
        {
            if (OperatingSystem.IsWindows())
                return Finder.Split(';', StringSplitOptions.RemoveEmptyEntries);

            return [Path.GetFileName(Finder)];
        }
    }

    public class DiffMergeTool(string exec, string cmd)
    {
        public string Exec { get; } = exec;
        public string Cmd { get; } = cmd;
    }
}
