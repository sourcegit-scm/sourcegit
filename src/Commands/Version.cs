namespace SourceGit.Commands {
    public class Version : Command {
        public Version() {
            Args = "--version";
            RaiseError = false;
        }

        public string Query() {
            var rs = ReadToEnd();
            if (!rs.IsSuccess || string.IsNullOrWhiteSpace(rs.StdOut)) return string.Empty;
            return rs.StdOut.Trim().Substring("git version ".Length);
        }
    }
}
