using System.Collections.Generic;
using System.Text;

namespace SourceGit.Commands
{
    public class Restore : Command
    {
        /// <summary>
        ///     Only used to discard all changes in the working directory and staged area.
        /// </summary>
        /// <param name="repo"></param>
        public Restore(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = "restore --source=HEAD --staged --worktree --recurse-submodules .";
        }

        /// <summary>
        ///     Discard changes with git (&lt; 2.25.0) that does not support the `--pathspec-from-file` option.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="files"></param>
        /// <param name="extra"></param>
        public Restore(string repo, List<string> files, string extra)
        {
            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder();
            builder.Append("restore ");
            if (!string.IsNullOrEmpty(extra))
                builder.Append(extra).Append(" ");
            builder.Append("--");
            foreach (var f in files)
                builder.Append(' ').Append('"').Append(f).Append('"');
            Args = builder.ToString();
        }

        /// <summary>
        ///     Discard changes with git (&gt;= 2.25.0) that supports the `--pathspec-from-file` option.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="pathspecFile"></param>
        /// <param name="extra"></param>
        public Restore(string repo, string pathspecFile, string extra)
        {
            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder();
            builder.Append("restore ");
            if (!string.IsNullOrEmpty(extra))
                builder.Append(extra).Append(" ");
            builder.Append("--pathspec-from-file=\"").Append(pathspecFile).Append('"');
            Args = builder.ToString();
        }
    }
}
