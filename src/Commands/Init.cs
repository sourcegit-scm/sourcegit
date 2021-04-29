namespace SourceGit.Commands {

    /// <summary>
    ///     初始化Git仓库
    /// </summary>
    public class Init : Command {

        public Init(string workDir) {
            Cwd = workDir;
            Args = "init -q";
        }
    }
}
