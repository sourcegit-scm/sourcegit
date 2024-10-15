using System;
using System.Collections.Generic;

namespace SourceGit.Commands
{
    public class QueryTags : Command
    {
        public QueryTags(string repo)
        {
            Context = repo;
            WorkingDirectory = repo;
            Args = "tag -l --sort=-creatordate --format=\"%(refname)%00%(objectname)%00%(*objectname)\"";
        }

        public List<Models.Tag> Result()
        {
            var tags = new List<Models.Tag>();
            var rs = ReadToEnd();
            if (!rs.IsSuccess)
                return tags;

            var lines = rs.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var tag = ParseLine(line);
                if (tag != null)
                    tags.Add(tag);
            }

            return tags;
        }

        private Models.Tag ParseLine(string line)
        {
            var subs = line.Split('\0');
            if (subs.Length != 3)
                return null;

            var tag = new Models.Tag();
            tag.Name = subs[0].Substring(10);
            tag.SHA = string.IsNullOrEmpty(subs[2]) ? subs[1] : subs[2];
            return tag;
        }
    }
}
