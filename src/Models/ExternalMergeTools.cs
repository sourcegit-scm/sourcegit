using System;
using System.Collections.Generic;

namespace SourceGit.Models {
    public class ExternalMergeTools {
        public int Type { get; set; }
        public string Name { get; set; }
        public string Exec { get; set; }
        public string Cmd { get; set; }
        public string DiffCmd { get; set; }

        public static List<ExternalMergeTools> Supported;

        static ExternalMergeTools() {
            if (OperatingSystem.IsWindows()) {
                Supported = new List<ExternalMergeTools>() {
                    new ExternalMergeTools(0, "Custom", "", "", ""),
                    new ExternalMergeTools(1, "Visual Studio Code", "Code.exe", "-n --wait \"$MERGED\"", "-n --wait --diff \"$LOCAL\" \"$REMOTE\""),
                    new ExternalMergeTools(2, "Visual Studio 2017/2019", "vsDiffMerge.exe", "\"$REMOTE\" \"$LOCAL\" \"$BASE\" \"$MERGED\" /m", "\"$LOCAL\" \"$REMOTE\""),
                    new ExternalMergeTools(3, "Tortoise Merge", "TortoiseMerge.exe;TortoiseGitMerge.exe", "-base:\"$BASE\" -theirs:\"$REMOTE\" -mine:\"$LOCAL\" -merged:\"$MERGED\"", "-base:\"$LOCAL\" -theirs:\"$REMOTE\""),
                    new ExternalMergeTools(4, "KDiff3", "kdiff3.exe", "\"$REMOTE\" -b \"$BASE\" \"$LOCAL\" -o \"$MERGED\"", "\"$LOCAL\" \"$REMOTE\""),
                    new ExternalMergeTools(5, "Beyond Compare 4", "BComp.exe", "\"$REMOTE\" \"$LOCAL\" \"$BASE\" \"$MERGED\"", "\"$LOCAL\" \"$REMOTE\""),
                    new ExternalMergeTools(6, "WinMerge", "WinMergeU.exe", "-u -e \"$REMOTE\" \"$LOCAL\" \"$MERGED\"", "-u -e \"$LOCAL\" \"$REMOTE\""),
                };
            }
        }

        public ExternalMergeTools(int type, string name, string exec, string cmd, string diffCmd) {
            Type = type;
            Name = name;
            Exec = exec;
            Cmd = cmd;
            DiffCmd = diffCmd;
        }
    }
}
