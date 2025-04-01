namespace SourceGit.Models
{
    public static class GitVersions
    {
        /// <summary>
        ///     The minimal version of Git that required by this app.
        /// </summary>
        public static readonly System.Version MINIMAL = new System.Version(2, 23, 0);

        /// <summary>
        ///     The minimal version of Git that supports the `add` command with the `--pathspec-from-file` option.
        /// </summary>
        public static readonly System.Version ADD_WITH_PATHSPECFILE = new System.Version(2, 25, 0);

        /// <summary>
        ///     The minimal version of Git that supports the `stash push` command with the `--pathspec-from-file` option.
        /// </summary>
        public static readonly System.Version STASH_PUSH_WITH_PATHSPECFILE = new System.Version(2, 26, 0);

        /// <summary>
        ///     The minimal version of Git that supports the `stash push` command with the `--staged` option.
        /// </summary>
        public static readonly System.Version STASH_PUSH_ONLY_STAGED = new System.Version(2, 35, 0);

        /// <summary>
        ///     The minimal version of Git that supports the `stash show` command with the `-u` option.
        /// </summary>
        public static readonly System.Version STASH_SHOW_WITH_UNTRACKED = new System.Version(2, 32, 0);
    }
}
