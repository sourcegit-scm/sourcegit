using System;
using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class GetFileChangeForAI : Command
    {
        public GetFileChangeForAI(string repo, string file, string originalFile)
        {
            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder();
            builder.Append("diff --no-color --no-ext-diff --diff-algorithm=minimal --cached -- ");
            if (!string.IsNullOrEmpty(originalFile) && !file.Equals(originalFile, StringComparison.Ordinal))
                builder.Append(originalFile.Quoted()).Append(' ');
            builder.Append(file.Quoted());

            Args = builder.ToString();
        }

        public async Task<Result> ReadAsync()
        {
            return await ReadToEndAsync().ConfigureAwait(false);
        }
    }
}
