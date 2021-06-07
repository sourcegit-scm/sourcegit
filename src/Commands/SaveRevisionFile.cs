using System.Diagnostics;
using System.IO;

namespace SourceGit.Commands {
    /// <summary>
    ///     保存指定版本的文件
    /// </summary>
    public class SaveRevisionFile {
        private string cwd = "";
        private string bat = "";

        public SaveRevisionFile(string repo, string path, string sha, string saveTo) {
            var tmp = Path.GetTempFileName();
            var cmd = $"\"{Models.Preference.Instance.Git.Path}\" --no-pager ";

            var isLFS = new LFS(repo).IsFiltered(path);
            if (isLFS) {
                cmd += $"show {sha}:\"{path}\" > {tmp}.lfs\n";
                cmd += $"\"{Models.Preference.Instance.Git.Path}\" --no-pager lfs smudge < {tmp}.lfs > \"{saveTo}\"\n";
            } else {
                cmd += $"show {sha}:\"{path}\" > \"{saveTo}\"\n";
            }

            cwd = repo;
            bat = tmp + ".bat";

            File.WriteAllText(bat, cmd);
        }

        public void Exec() {
            var starter = new ProcessStartInfo();
            starter.FileName = bat;
            starter.WorkingDirectory = cwd;
            starter.CreateNoWindow = true;
            starter.WindowStyle = ProcessWindowStyle.Hidden;

            var proc = Process.Start(starter);
            proc.WaitForExit();
            proc.Close();

            File.Delete(bat);
        }
    }
}
