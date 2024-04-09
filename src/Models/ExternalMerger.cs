using System;
using System.Collections.Generic;
using System.IO;

namespace SourceGit.Models
{
    public class ExternalMerger
    {
        public int Type { get; set; }
        public string Name { get; set; }
        public string Exec { get; set; }
        public string Cmd { get; set; }
        public string DiffCmd { get; set; }

        public static readonly List<ExternalMerger> Supported;

        static ExternalMerger()
        {
            if (OperatingSystem.IsWindows())
            {
                Supported = new List<ExternalMerger>() {
                    new ExternalMerger(0, "Custom", "", "", ""),
                    new ExternalMerger(1, "Visual Studio Code", "Code.exe", "-n --wait \"$MERGED\"", "-n --wait --diff \"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(2, "Visual Studio Code - Insiders", "Code - Insiders.exe", "-n --wait \"$MERGED\"", "-n --wait --diff \"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(3, "Visual Studio 2017/2019/2022", "vsDiffMerge.exe", "\"$REMOTE\" \"$LOCAL\" \"$BASE\" \"$MERGED\" /m", "\"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(4, "Tortoise Merge", "TortoiseMerge.exe;TortoiseGitMerge.exe", "-base:\"$BASE\" -theirs:\"$REMOTE\" -mine:\"$LOCAL\" -merged:\"$MERGED\"", "-base:\"$LOCAL\" -theirs:\"$REMOTE\""),
                    new ExternalMerger(5, "KDiff3", "kdiff3.exe", "\"$REMOTE\" -b \"$BASE\" \"$LOCAL\" -o \"$MERGED\"", "\"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(6, "Beyond Compare", "BComp.exe", "\"$REMOTE\" \"$LOCAL\" \"$BASE\" \"$MERGED\"", "\"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(7, "WinMerge", "WinMergeU.exe", "-u -e \"$REMOTE\" \"$LOCAL\" \"$MERGED\"", "-u -e \"$LOCAL\" \"$REMOTE\""),
                };
            }
            else if (OperatingSystem.IsMacOS())
            {
                Supported = new List<ExternalMerger>() {
                    new ExternalMerger(0, "Custom", "", "", ""),
                    new ExternalMerger(1, "FileMerge", "/usr/bin/opendiff", "\"$BASE\" \"$LOCAL\" \"$REMOTE\" -ancestor \"$MERGED\"", "\"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(2, "Visual Studio Code", "/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code", "-n --wait \"$MERGED\"", "-n --wait --diff \"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(3, "Visual Studio Code - Insiders", "/Applications/Visual Studio Code - Insiders.app/Contents/Resources/app/bin/code", "-n --wait \"$MERGED\"", "-n --wait --diff \"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(4, "KDiff3", "/Applications/kdiff3.app/Contents/MacOS/kdiff3", "\"$REMOTE\" -b \"$BASE\" \"$LOCAL\" -o \"$MERGED\"", "\"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(5, "Beyond Compare", "/Applications/Beyond Compare.app/Contents/MacOS/bcomp", "\"$REMOTE\" \"$LOCAL\" \"$BASE\" \"$MERGED\"", "\"$LOCAL\" \"$REMOTE\""),
                };
            }
            else if (OperatingSystem.IsLinux())
            {
                Supported = new List<ExternalMerger>() {
                    new ExternalMerger(0, "Custom", "", "", ""),
                    new ExternalMerger(1, "Visual Studio Code", "/usr/share/code/code", "-n --wait \"$MERGED\"", "-n --wait --diff \"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(2, "Visual Studio Code - Insiders", "/usr/share/code-insiders/code-insiders", "-n --wait \"$MERGED\"", "-n --wait --diff \"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(3, "KDiff3", "/usr/bin/kdiff3", "\"$REMOTE\" -b \"$BASE\" \"$LOCAL\" -o \"$MERGED\"", "\"$LOCAL\" \"$REMOTE\""),
                    new ExternalMerger(4, "Beyond Compare", "/usr/bin/bcomp", "\"$REMOTE\" \"$LOCAL\" \"$BASE\" \"$MERGED\"", "\"$LOCAL\" \"$REMOTE\""),
                };
            }
            else
            {
                Supported = new List<ExternalMerger>() {
                    new ExternalMerger(0, "Custom", "", "", ""),
                };
            }
        }

        public ExternalMerger(int type, string name, string exec, string cmd, string diffCmd)
        {
            Type = type;
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
