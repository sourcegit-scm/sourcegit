using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryRefsContainsCommit : Command
    {
        public QueryRefsContainsCommit(string repo, string commit)
        {
            WorkingDirectory = repo;
            RaiseError = false;
            Args = $"for-each-ref --format=\"%(refname)\" --contains {commit}";
        }

        public List<Models.Decorator> Result()
        {
            var rs = new List<Models.Decorator>();

            var output = ReadToEnd();
            if (!output.IsSuccess)
                return rs;

            var lines = output.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.EndsWith("/HEAD", StringComparison.Ordinal))
                    continue;

                if (line.StartsWith("refs/heads/", StringComparison.Ordinal))
                    rs.Add(new() { Name = line.Substring("refs/heads/".Length), Type = Models.DecoratorType.LocalBranchHead });
                else if (line.StartsWith("refs/remotes/", StringComparison.Ordinal))
                    rs.Add(new() { Name = line.Substring("refs/remotes/".Length), Type = Models.DecoratorType.RemoteBranchHead });
                else if (line.StartsWith("refs/tags/", StringComparison.Ordinal))
                    rs.Add(new() { Name = line.Substring("refs/tags/".Length), Type = Models.DecoratorType.Tag });
            }

            return rs;
        }

        public async Task<List<Models.Decorator>> ResultAsync()
        {
            var rs = new List<Models.Decorator>();

            var output = await ReadToEndAsync();
            if (!output.IsSuccess)
                return rs;

            var lines = output.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.EndsWith("/HEAD", StringComparison.Ordinal))
                    continue;

                if (line.StartsWith("refs/heads/", StringComparison.Ordinal))
                    rs.Add(new() { Name = line.Substring("refs/heads/".Length), Type = Models.DecoratorType.LocalBranchHead });
                else if (line.StartsWith("refs/remotes/", StringComparison.Ordinal))
                    rs.Add(new() { Name = line.Substring("refs/remotes/".Length), Type = Models.DecoratorType.RemoteBranchHead });
                else if (line.StartsWith("refs/tags/", StringComparison.Ordinal))
                    rs.Add(new() { Name = line.Substring("refs/tags/".Length), Type = Models.DecoratorType.Tag });
            }

            return rs;
        }
    }
}
