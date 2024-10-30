﻿namespace SourceGit.Commands
{
    public class QueryCommitSignInfo : Command
    {
        public QueryCommitSignInfo(string repo, string sha, bool useFakeSignersFile)
        {
            WorkingDirectory = repo;
            Context = repo;

            const string baseArgs = "show --no-show-signature --pretty=format:\"%G?%n%GS%n%GK\" -s";
            const string fakeSignersFileArg = "-c gpg.ssh.allowedSignersFile=/dev/null";
            Args = $"{(useFakeSignersFile ? fakeSignersFileArg : string.Empty)} {baseArgs} {sha}";
        }

        public Models.CommitSignInfo Result()
        {
            var rs = ReadToEnd();
            if (!rs.IsSuccess)
                return null;

            var raw = rs.StdOut.Trim();
            if (raw.Length <= 1)
                return null;

            var lines = raw.Split('\n');
            return new Models.CommitSignInfo()
            {
                VerifyResult = lines[0][0],
                Signer = string.IsNullOrEmpty(lines[1]) ? "<UnKnown>" : lines[1],
                Key = lines[2]
            };

        }
    }
}
