using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class Checkout : Command
    {
        public Checkout(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
        }

        public bool Branch(string branch, bool force)
        {
            var builder = new StringBuilder();
            builder.Append("checkout --progress ");
            if (force)
                builder.Append("--force ");
            builder.Append(branch);

            Args = builder.ToString();
            return Exec();
        }

        public bool Branch(string branch, string basedOn, bool force, bool allowOverwrite)
        {
            var builder = new StringBuilder();
            builder.Append("checkout --progress ");
            if (force)
                builder.Append("--force ");
            builder.Append(allowOverwrite ? "-B " : "-b ");
            builder.Append(branch);
            builder.Append(" ");
            builder.Append(basedOn);

            Args = builder.ToString();
            return Exec();
        }

        public bool Commit(string commitId, bool force)
        {
            var option = force ? "--force" : string.Empty;
            Args = $"checkout {option} --detach --progress {commitId}";
            return Exec();
        }

        public bool UseTheirs(List<string> files)
        {
            var builder = new StringBuilder();
            builder.Append("checkout --theirs --");
            foreach (var f in files)
            {
                builder.Append(" \"");
                builder.Append(f);
                builder.Append("\"");
            }
            Args = builder.ToString();
            return Exec();
        }

        public bool UseMine(List<string> files)
        {
            var builder = new StringBuilder();
            builder.Append("checkout --ours --");
            foreach (var f in files)
            {
                builder.Append(" \"");
                builder.Append(f);
                builder.Append("\"");
            }
            Args = builder.ToString();
            return Exec();
        }

        public bool FileWithRevision(string file, string revision)
        {
            Args = $"checkout --no-overlay {revision} -- \"{file}\"";
            return Exec();
        }

        public async Task<bool> BranchAsync(string branch, bool force)
        {
            var builder = new StringBuilder();
            builder.Append("checkout --progress ");
            if (force)
                builder.Append("--force ");
            builder.Append(branch);

            Args = builder.ToString();
            return await ExecAsync();
        }

        public async Task<bool> BranchAsync(string branch, string basedOn, bool force, bool allowOverwrite)
        {
            var builder = new StringBuilder();
            builder.Append("checkout --progress ");
            if (force)
                builder.Append("--force ");
            builder.Append(allowOverwrite ? "-B " : "-b ");
            builder.Append(branch);
            builder.Append(" ");
            builder.Append(basedOn);

            Args = builder.ToString();
            return await ExecAsync();
        }

        public async Task<bool> CommitAsync(string commitId, bool force)
        {
            var option = force ? "--force" : string.Empty;
            Args = $"checkout {option} --detach --progress {commitId}";
            return await ExecAsync();
        }

        public async Task<bool> UseTheirsAsync(List<string> files)
        {
            var builder = new StringBuilder();
            builder.Append("checkout --theirs --");
            foreach (var f in files)
            {
                builder.Append(" \"");
                builder.Append(f);
                builder.Append("\"");
            }
            Args = builder.ToString();
            return await ExecAsync();
        }

        public async Task<bool> UseMineAsync(List<string> files)
        {
            var builder = new StringBuilder();
            builder.Append("checkout --ours --");
            foreach (var f in files)
            {
                builder.Append(" \"");
                builder.Append(f);
                builder.Append("\"");
            }

            Args = builder.ToString();
            return await ExecAsync();
        }

        public async Task<bool> FileWithRevisionAsync(string file, string revision)
        {
            Args = $"checkout --no-overlay {revision} -- \"{file}\"";
            return await ExecAsync();
        }
    }
}
