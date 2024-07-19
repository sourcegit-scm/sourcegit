using System;

namespace SourceGit.Commands
{
    public class QueryTrackStatus : Command
    {
        public QueryTrackStatus(string repo, string local, string upstream) 
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"rev-list --left-right {local}...{upstream}";
        }

        public Models.BranchTrackStatus Result()
        {
            var status = new Models.BranchTrackStatus();

            var rs = ReadToEnd();
            if (!rs.IsSuccess)
                return status;

            var lines = rs.StdOut.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line[0] == '>')
                    status.Behind.Add(line.Substring(1));
                else
                    status.Ahead.Add(line.Substring(1));
            }

            return status;
        }
    }
}
