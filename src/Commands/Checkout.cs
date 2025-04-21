﻿using System.Collections.Generic;
using System.Text;

namespace SourceGit.Commands
{
    public class Checkout : Command
    {
        public Checkout(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
        }

        public bool Branch(string branch)
        {
            Args = $"checkout --recurse-submodules --progress {branch}";
            return Exec();
        }

        public bool Branch(string branch, string basedOn)
        {
            Args = $"checkout --recurse-submodules --progress -b {branch} {basedOn}";
            return Exec();
        }

        public bool UseTheirs(List<string> files)
        {
            var builder = new StringBuilder();
            builder.Append("checkout --theirs --");
            foreach (var f in files)
            {
                builder.Append(" \"");
                builder.Append(f);
                builder.Append("\"");
            }
            Args = builder.ToString();
            return Exec();
        }

        public bool UseMine(List<string> files)
        {
            var builder = new StringBuilder();
            builder.Append("checkout --ours --");
            foreach (var f in files)
            {
                builder.Append(" \"");
                builder.Append(f);
                builder.Append("\"");
            }
            Args = builder.ToString();
            return Exec();
        }

        public bool FileWithRevision(string file, string revision)
        {
            Args = $"checkout --no-overlay {revision} -- \"{file}\"";
            return Exec();
        }

        public bool Commit(string commitId)
        {
            Args = $"checkout --detach --progress {commitId}";
            return Exec();
        }
    }
}
