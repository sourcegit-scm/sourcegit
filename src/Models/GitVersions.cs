namespace SourceGit.Models
{
    public static class GitVersions
    {
        /// <summary>
        ///     The minimal version of Git that required by this app.
        /// </summary>
        public static readonly System.Version MINIMAL = new(2, 25, 1);

        /// <summary>
        ///     The minimal version of Git that supports the `stash push` command with the `--pathspec-from-file` option.
        /// </summary>
        public static readonly System.Version STASH_PUSH_WITH_PATHSPECFILE = new(2, 26, 0);

        /// <summary>
        ///     The minimal version of Git that supports the `stash push` command with the `--staged` option.
        /// </summary>
        public static readonly System.Version STASH_PUSH_ONLY_STAGED = new(2, 35, 0);
    }
}
