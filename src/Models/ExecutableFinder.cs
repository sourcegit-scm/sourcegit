using System.Runtime.InteropServices;
using System.Text;

namespace SourceGit.Models {
    /// <summary>
    ///     用于在PATH中检测可执行文件
    /// </summary>
    public class ExecutableFinder {

        // https://docs.microsoft.com/en-us/windows/desktop/api/shlwapi/nf-shlwapi-pathfindonpathw
        // https://www.pinvoke.net/default.aspx/shlwapi.PathFindOnPath
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, SetLastError = false)]
        private static extern bool PathFindOnPath([In, Out] StringBuilder pszFile, [In] string[] ppszOtherDirs);

        /// <summary>
        ///     从PATH中找到可执行文件路径
        /// </summary>
        /// <param name="exec"></param>
        /// <returns></returns>
        public static string Find(string exec) {
            var builder = new StringBuilder(exec, 259);
            var rs = PathFindOnPath(builder, null);
            return rs ? builder.ToString() : null;
        }
    }
}
