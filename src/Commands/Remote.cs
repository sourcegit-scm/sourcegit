﻿using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class Remote : Command
    {
        public Remote(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
        }

        public bool Add(string name, string url)
        {
            Args = $"remote add {name} {url}";
            return Exec();
        }

        public bool Delete(string name)
        {
            Args = $"remote remove {name}";
            return Exec();
        }

        public bool Rename(string name, string to)
        {
            Args = $"remote rename {name} {to}";
            return Exec();
        }

        public bool Prune(string name)
        {
            Args = $"remote prune {name}";
            return Exec();
        }

        public string GetURL(string name, bool isPush)
        {
            Args = "remote get-url" + (isPush ? " --push " : " ") + name;

            var rs = ReadToEnd();
            return rs.IsSuccess ? rs.StdOut.Trim() : string.Empty;
        }

        public bool SetURL(string name, string url, bool isPush)
        {
            Args = "remote set-url" + (isPush ? " --push " : " ") + $"{name} {url}";
            return Exec();
        }

        public bool HasBranch(string remote, string branch)
        {
            SSHKey = new Config(WorkingDirectory).Get($"remote.{remote}.sshkey");
            Args = $"ls-remote {remote} {branch}";

            var rs = ReadToEnd();
            return rs.IsSuccess && rs.StdOut.Trim().Length > 0;
        }

        public async Task<bool> AddAsync(string name, string url)
        {
            Args = $"remote add {name} {url}";
            return await ExecAsync();
        }

        public async Task<bool> DeleteAsync(string name)
        {
            Args = $"remote remove {name}";
            return await ExecAsync();
        }

        public async Task<bool> RenameAsync(string name, string to)
        {
            Args = $"remote rename {name} {to}";
            return await ExecAsync();
        }

        public async Task<bool> PruneAsync(string name)
        {
            Args = $"remote prune {name}";
            return await ExecAsync();
        }

        public async Task<string> GetURLAsync(string name, bool isPush)
        {
            Args = "remote get-url" + (isPush ? " --push " : " ") + name;

            var rs = await ReadToEndAsync();
            return rs.IsSuccess ? rs.StdOut.Trim() : string.Empty;
        }

        public async Task<bool> SetURLAsync(string name, string url, bool isPush)
        {
            Args = "remote set-url" + (isPush ? " --push " : " ") + $"{name} {url}";
            return await ExecAsync();
        }

        public async Task<bool> HasBranchAsync(string remote, string branch)
        {
            SSHKey = await new Config(WorkingDirectory).GetAsync($"remote.{remote}.sshkey");
            Args = $"ls-remote {remote} {branch}";

            var rs = await ReadToEndAsync();
            return rs.IsSuccess && rs.StdOut.Trim().Length > 0;
        }
    }
}
