using System.Text;

namespace SourceGit.Commands
{
    public class Restore : Command
    {
        /// <summary>
        ///     Only used for single staged change.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="stagedChange"></param>
        public Restore(string repo, Models.Change stagedChange)
        {
            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder();
            builder.Append("restore --staged -- \"");
            builder.Append(stagedChange.Path);
            builder.Append('"');

            if (stagedChange.Index == Models.ChangeState.Renamed)
            {
                builder.Append(" \"");
                builder.Append(stagedChange.OriginalPath);
                builder.Append('"');
            }

            Args = builder.ToString();
        }

        /// <summary>
        ///     Restore changes given in a path-spec file.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="pathspecFile"></param>
        /// <param name="isStaged"></param>
        public Restore(string repo, string pathspecFile, bool isStaged)
        {
            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder();
            builder.Append("restore ");
            builder.Append(isStaged ? "--staged " : "--worktree --recurse-submodules ");
            builder.Append("--pathspec-from-file=\"");
            builder.Append(pathspecFile);
            builder.Append('"');

            Args = builder.ToString();
        }
    }
}
