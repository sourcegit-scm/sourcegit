using Microsoft.Win32;
using SourceGit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Git {

    /// <summary>
    ///     External merge tool
    /// </summary>
    public class MergeTool {

        /// <summary>
        ///     Display name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     Executable file name.
        /// </summary>
        public string ExecutableName { get; set; }

        /// <summary>
        ///     Command line parameter.
        /// </summary>
        public string Parameter { get; set; }

        /// <summary>
        ///     Auto finder.
        /// </summary>
        public Func<string> Finder { get; set; }

        /// <summary>
        ///     Is this merge tool configured.
        /// </summary>
        public bool IsConfigured => !string.IsNullOrEmpty(ExecutableName);

        /// <summary>
        ///     Supported merge tools.
        /// </summary>
        public static List<MergeTool> Supported = new List<MergeTool>() {
            new MergeTool("--", "", "", FindInvalid),
            new MergeTool("Araxis Merge", "Compare.exe", "/wait /merge /3 /a1 \"$BASE\" \"$REMOTE\" \"$LOCAL\" \"$MERGED\"", FindAraxisMerge),
            new MergeTool("Beyond Compare 4", "BComp.exe", "\"$REMOTE\" \"$LOCAL\" \"$BASE\" \"$MERGED\"", FindBCompare),
            new MergeTool("KDiff3", "kdiff3.exe", "\"$REMOTE\" -b \"$BASE\" \"$LOCAL\" -o \"$MERGED\"", FindKDiff3),
            new MergeTool("P4Merge", "p4merge.exe", "\"$BASE\" \"$REMOTE\" \"$LOCAL\" \"$MERGED\"", FindP4Merge),
            new MergeTool("Tortoise Merge", "TortoiseMerge.exe", "-base:\"$BASE\" -theirs:\"$REMOTE\" -mine:\"$LOCAL\" -merged:\"$MERGED\"", FindTortoiseMerge),
            new MergeTool("Visual Studio 2017/2019", "vsDiffMerge.exe", "\"$REMOTE\" \"$LOCAL\" \"$BASE\" \"$MERGED\" //m", FindVSMerge),
            new MergeTool("Visual Studio Code", "Code.exe", "-n --wait \"$MERGED\"", FindVSCode),
        };

        /// <summary>
        ///     Finder for invalid merge tool.
        /// </summary>
        /// <returns></returns>
        public static string FindInvalid() {
            return "--";
        }

        /// <summary>
        ///     Find araxis merge tool install path.
        /// </summary>
        /// <returns></returns>
        public static string FindAraxisMerge() {
            var path = @"C:\Program Files\Araxis\Araxis Merge\Compare.exe";
            if (File.Exists(path)) return path;
            return "";
        }

        /// <summary>
        ///     Find kdiff3.exe by registry.
        /// </summary>
        /// <returns></returns>
        public static string FindKDiff3() {
            var root = RegistryKey.OpenBaseKey(
                   RegistryHive.LocalMachine,
                   Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32);

            var kdiff = root.OpenSubKey(@"SOFTWARE\KDiff3\diff-ext");
            if (kdiff == null) return "";
            return kdiff.GetValue("diffcommand") as string;
        }

        /// <summary>
        ///     Finder for p4merge
        /// </summary>
        /// <returns></returns>
        public static string FindP4Merge() {
            var path = @"C:\Program Files\Perforce\p4merge.exe";
            if (File.Exists(path)) return path;
            return "";
        }

        /// <summary>
        ///     Find BComp.exe by registry.
        /// </summary>
        /// <returns></returns>
        public static string FindBCompare() {
            var root = RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine,
                    Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32);

            var bc = root.OpenSubKey(@"SOFTWARE\Scooter Software\Beyond Compare");
            if (bc == null) return "";

            var exec = bc.GetValue("ExePath") as string;
            var dir = Path.GetDirectoryName(exec);
            return $"{dir}\\BComp.exe";
        }

        /// <summary>
        ///     Find TortoiseMerge.exe by registry.
        /// </summary>
        /// <returns></returns>
        public static string FindTortoiseMerge() {
            var root = RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine,
                    Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32);

            var tortoiseSVN = root.OpenSubKey("SOFTWARE\\TortoiseSVN");
            if (tortoiseSVN == null) return "";
            return tortoiseSVN.GetValue("TMergePath") as string;
        }

        /// <summary>
        ///     Find vsDiffMerge.exe.
        /// </summary>
        /// <returns></returns>
        public static string FindVSMerge() {
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

        /// <summary>
        ///     Find VSCode executable file path.
        /// </summary>
        /// <returns></returns>
        public static string FindVSCode() {
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

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="exe"></param>
        /// <param name="param"></param>
        /// <param name="finder"></param>
        public MergeTool(string name, string exe, string param, Func<string> finder) {
            Name = name;
            ExecutableName = exe;
            Parameter = param;
            Finder = finder;
        }
    }
}
