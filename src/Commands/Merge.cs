using System.Collections.Generic;
using System.Text;

namespace SourceGit.Commands
{
    public class Merge : Command
    {
        public Merge(string repo, string source, string mode, bool edit)
        {
            WorkingDirectory = repo;
            Context = repo;
            Editor = EditorType.CoreEditor;

            var builder = new StringBuilder();
            builder.Append("merge --progress ");
            builder.Append(edit ? "--edit " : "--no-edit ");
            builder.Append(source);
            builder.Append(' ');
            builder.Append(mode);

            Args = builder.ToString();
        }

        public Merge(string repo, List<string> targets, bool autoCommit, string strategy)
        {
            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder();
            builder.Append("merge --progress ");
            if (!string.IsNullOrEmpty(strategy))
                builder.Append("--strategy=").Append(strategy).Append(' ');
            if (!autoCommit)
                builder.Append("--no-commit ");

            foreach (var t in targets)
            {
                builder.Append(t);
                builder.Append(' ');
            }

            Args = builder.ToString();
        }
    }
}
