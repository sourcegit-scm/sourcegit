using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class Stash : Command
    {
        public Stash(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
        }

        public async Task<bool> PushAsync(string message, bool includeUntracked = true, bool keepIndex = false)
        {
            var builder = new StringBuilder();
            builder.Append("stash push ");
            if (includeUntracked)
                builder.Append("--include-untracked ");
            if (keepIndex)
                builder.Append("--keep-index ");
            builder.Append("-m \"");
            builder.Append(message);
            builder.Append("\"");

            Args = builder.ToString();
            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task<bool> PushAsync(string message, List<Models.Change> changes, bool keepIndex)
        {
            var builder = new StringBuilder();
            builder.Append("stash push --include-untracked ");
            if (keepIndex)
                builder.Append("--keep-index ");
            builder.Append("-m \"");
            builder.Append(message);
            builder.Append("\" -- ");

            foreach (var c in changes)
                builder.Append($"\"{c.Path}\" ");

            Args = builder.ToString();
            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task<bool> PushAsync(string message, string pathspecFromFile, bool keepIndex)
        {
            var builder = new StringBuilder();
            builder.Append("stash push --include-untracked --pathspec-from-file=\"");
            builder.Append(pathspecFromFile);
            builder.Append("\" ");
            if (keepIndex)
                builder.Append("--keep-index ");
            builder.Append("-m \"");
            builder.Append(message);
            builder.Append("\"");

            Args = builder.ToString();
            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task<bool> PushOnlyStagedAsync(string message, bool keepIndex)
        {
            var builder = new StringBuilder();
            builder.Append("stash push --staged ");
            if (keepIndex)
                builder.Append("--keep-index ");
            builder.Append("-m \"");
            builder.Append(message);
            builder.Append("\"");
            Args = builder.ToString();
            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task<bool> ApplyAsync(string name, bool restoreIndex)
        {
            var opts = restoreIndex ? "--index" : string.Empty;
            Args = $"stash apply -q {opts} \"{name}\"";
            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task<bool> PopAsync(string name)
        {
            Args = $"stash pop -q --index \"{name}\"";
            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task<bool> DropAsync(string name)
        {
            Args = $"stash drop -q \"{name}\"";
            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task<bool> ClearAsync()
        {
            Args = "stash clear";
            return await ExecAsync().ConfigureAwait(false);
        }
    }
}
