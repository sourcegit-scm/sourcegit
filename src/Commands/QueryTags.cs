using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryTags : Command
    {
        public QueryTags(string repo)
        {
            _boundary = $"----- BOUNDARY OF TAGS {Guid.NewGuid()} -----";

            Context = repo;
            WorkingDirectory = repo;
            Args = $"tag -l --format=\"{_boundary}%(refname)%00%(objecttype)%00%(objectname)%00%(*objectname)%00%(taggername)±%(taggeremail)%00%(creatordate:unix)%00%(contents:subject)%0a%0a%(contents:body)\"";
        }

        public async Task<List<Models.Tag>> GetResultAsync()
        {
            var tags = new List<Models.Tag>();
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return tags;

            var records = rs.StdOut.Split(_boundary, StringSplitOptions.RemoveEmptyEntries);
            foreach (var record in records)
            {
                var subs = record.Split('\0');
                if (subs.Length != 7)
                    continue;

                var name = subs[0].Substring(10);
                var message = subs[6].Trim();
                if (!string.IsNullOrEmpty(message) && message.Equals(name, StringComparison.Ordinal))
                    message = null;

                ulong.TryParse(subs[5], out var creatorDate);

                tags.Add(new Models.Tag()
                {
                    Name = name,
                    IsAnnotated = subs[1].Equals("tag", StringComparison.Ordinal),
                    SHA = string.IsNullOrEmpty(subs[3]) ? subs[2] : subs[3],
                    Creator = Models.User.FindOrAdd(subs[4]),
                    CreatorDate = creatorDate,
                    Message = message,
                });
            }

            return tags;
        }

        private readonly string _boundary;
    }
}
