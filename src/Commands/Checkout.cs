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

        public async Task<bool> BranchAsync(string branch, bool force)
        {
            var builder = new StringBuilder();
            builder.Append("checkout --progress ");
            if (force)
                builder.Append("--force ");
            builder.Append(branch);

            Args = builder.ToString();
            return await ExecAsync().ConfigureAwait(false);
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
            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task<bool> CommitAsync(string commitId, bool force)
        {
            var option = force ? "--force" : string.Empty;
            Args = $"checkout {option} --detach --progress {commitId}";
            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task<bool> UseTheirsAsync(List<string> files)
        {
            var builder = new StringBuilder();
            builder.Append("checkout --theirs --");
            foreach (var f in files)
                builder.Append(' ').Append(f.Quoted());
            Args = builder.ToString();
            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task<bool> UseMineAsync(List<string> files)
        {
            var builder = new StringBuilder();
            builder.Append("checkout --ours --");
            foreach (var f in files)
                builder.Append(' ').Append(f.Quoted());

            Args = builder.ToString();
            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task<bool> FileWithRevisionAsync(string file, string revision)
        {
            Args = $"checkout --no-overlay {revision} -- {file.Quoted()}";
            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task<bool> MultipleFilesWithRevisionAsync(List<string> files, string revision)
        {
            var builder = new StringBuilder();
            builder
                .Append("checkout --no-overlay ")
                .Append(revision)
                .Append(" --");

            foreach (var f in files)
                builder.Append(' ').Append(f.Quoted());

            Args = builder.ToString();
            return await ExecAsync().ConfigureAwait(false);
        }
    }
}
