using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;

namespace SourceGit.Models {

    /// <summary>
    ///     外部合并工具
    /// </summary>
    public class MergeTool {
        public int Type { get; set; }
        public string Name { get; set; }
        public string Exec { get; set; }
        public string Cmd { get; set; }
        public Func<string> Finder { get; set; }

        public static List<MergeTool> Supported = new List<MergeTool>() {
            new MergeTool(0, "--", "", "", () => ""),
            new MergeTool(1, "Visual Studio Code", "Code.exe", "-n --wait \"$MERGED\"", FindVSCode),
            new MergeTool(2, "Visual Studio 2017/2019", "vsDiffMerge.exe", "\"$REMOTE\" \"$LOCAL\" \"$BASE\" \"$MERGED\" //m", FindVSMerge),
            new MergeTool(3, "Tortoise Merge", "TortoiseMerge.exe", "-base:\"$BASE\" -theirs:\"$REMOTE\" -mine:\"$LOCAL\" -merged:\"$MERGED\"", FindTortoiseMerge),
            new MergeTool(4, "KDiff3", "kdiff3.exe", "\"$REMOTE\" -b \"$BASE\" \"$LOCAL\" -o \"$MERGED\"", FindKDiff3),
            new MergeTool(5, "Beyond Compare 4", "BComp.exe", "\"$REMOTE\" \"$LOCAL\" \"$BASE\" \"$MERGED\"", FindBCompare),
        };
        
        public MergeTool(int type, string name, string exec, string cmd, Func<string> finder) {
            Type = type;
            Name = name;
            Exec = exec;
            Cmd = cmd;
            Finder = finder;
        }

        private static string FindVSCode() {
            var root = RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine,
                    Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32);

            var vscode = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{C26E74D1-022E-4238-8B9D-1E7564A36CC9}_is1");
            if (vscode != null) {
                return vscode.GetValue("DisplayIcon") as string;
            }

            vscode = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{1287CAD5-7C8D-410D-88B9-0D1EE4A83FF2}_is1");
            if (vscode != null) {
                return vscode.GetValue("DisplayIcon") as string;
            }

            vscode = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{F8A2A208-72B3-4D61-95FC-8A65D340689B}_is1");
            if (vscode != null) {
                return vscode.GetValue("DisplayIcon") as string;
            }

            vscode = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{EA457B21-F73E-494C-ACAB-524FDE069978}_is1");
            if (vscode != null) {
                return vscode.GetValue("DisplayIcon") as string;
            }

            return "";
        }

        private static string FindVSMerge() {
            var dir = @"C:\Program Files (x86)\Microsoft Visual Studio";
            if (Directory.Exists($"{dir}\\2019")) {
                dir += "\\2019";
            } else if (Directory.Exists($"{dir}\\2017")) {
                dir += "\\2017";
            } else {
                return "";
            }

            if (Directory.Exists($"{dir}\\Community")) {
                dir += "\\Community";
            } else if (Directory.Exists($"{dir}\\Enterprise")) {
                dir += "\\Enterprise";
            } else if (Directory.Exists($"{dir}\\Professional")) {
                dir += "\\Professional";
            } else {
                return "";
            }

            return $"{dir}\\Common7\\IDE\\CommonExtensions\\Microsoft\\TeamFoundation\\Team Explorer\\vsDiffMerge.exe";
        }

        private static string FindTortoiseMerge() {
            var root = RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine,
                    Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32);

            var tortoiseSVN = root.OpenSubKey("SOFTWARE\\TortoiseSVN");
            if (tortoiseSVN == null) return "";
            return tortoiseSVN.GetValue("TMergePath") as string;
        }

        private static string FindKDiff3() {
            var root = RegistryKey.OpenBaseKey(
                   RegistryHive.LocalMachine,
                   Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32);

            var kdiff = root.OpenSubKey(@"SOFTWARE\KDiff3\diff-ext");
            if (kdiff == null) return "";
            return kdiff.GetValue("diffcommand") as string;
        }

        private static string FindBCompare() {
            var root = RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine,
                    Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32);

            var bc = root.OpenSubKey(@"SOFTWARE\Scooter Software\Beyond Compare");
            if (bc == null) return "";

            var exec = bc.GetValue("ExePath") as string;
            var dir = Path.GetDirectoryName(exec);
            return $"{dir}\\BComp.exe";
        }
    }
}
