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
            builder.Append("restore --staged -- ").Append(stagedChange.Path.Quoted());

            if (stagedChange.Index == Models.ChangeState.Renamed)
                builder.Append(' ').Append(stagedChange.OriginalPath.Quoted());

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
            builder.Append("--pathspec-from-file=").Append(pathspecFile.Quoted());

            Args = builder.ToString();
        }
    }
}
