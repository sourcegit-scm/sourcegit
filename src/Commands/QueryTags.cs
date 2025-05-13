using System;
using System.Collections.Generic;

namespace SourceGit.Commands
{
    public class QueryTags : Command
    {
        public QueryTags(string repo)
        {
            _boundary = $"----- BOUNDARY OF TAGS {Guid.NewGuid()} -----";

            Context = repo;
            WorkingDirectory = repo;
            Args = $"tag -l --format=\"{_boundary}%(refname)%00%(objecttype)%00%(objectname)%00%(*objectname)%00%(creatordate:unix)%00%(contents:subject)%0a%0a%(contents:body)\"";
        }

        public List<Models.Tag> Result()
        {
            var tags = new List<Models.Tag>();
            var rs = ReadToEnd();
            if (!rs.IsSuccess)
                return tags;

            var records = rs.StdOut.Split(_boundary, StringSplitOptions.RemoveEmptyEntries);
            foreach (var record in records)
            {
                var subs = record.Split('\0', StringSplitOptions.None);
                if (subs.Length != 6)
                    continue;

                var name = subs[0].Substring(10);
                var message = subs[5].Trim();
                if (!string.IsNullOrEmpty(message) && message.Equals(name, StringComparison.Ordinal))
                    message = null;

                tags.Add(new Models.Tag()
                {
                    Name = name,
                    IsAnnotated = subs[1].Equals("tag", StringComparison.Ordinal),
                    SHA = string.IsNullOrEmpty(subs[3]) ? subs[2] : subs[3],
                    CreatorDate = ulong.Parse(subs[4]),
                    Message = message,
                });
            }

            return tags;
        }

        private string _boundary = string.Empty;
    }
}
