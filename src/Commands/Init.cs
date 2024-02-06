namespace SourceGit.Commands {
    public class Init : Command {
        public Init(string ctx, string dir) {
            Context = ctx;
            WorkingDirectory = dir;
            Args = "init -q";
        }
    }
}
