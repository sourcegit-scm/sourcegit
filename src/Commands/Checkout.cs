﻿using System;
using System.Collections.Generic;
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

        public bool Target(string target, Action<string> onProgress)
        {
            Args = $"checkout --progress {target}";
            TraitErrorAsOutput = true;
            _outputHandler = onProgress;
            return Exec();
        }

        public bool Branch(string branch, string basedOn, Action<string> onProgress)
        {
            Args = $"checkout --progress -b {branch} {basedOn}";
            TraitErrorAsOutput = true;
            _outputHandler = onProgress;
            return Exec();
        }

        public bool UseTheirs(List<string> files)
        {
            StringBuilder builder = new StringBuilder();
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
            StringBuilder builder = new StringBuilder();
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
            Args = $"checkout {revision} -- \"{file}\"";
            return Exec();
        }

        public bool Files(List<string> files)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("checkout -f -q --");
            foreach (var f in files)
            {
                builder.Append(" \"");
                builder.Append(f);
                builder.Append("\"");
            }
            Args = builder.ToString();
            return Exec();
        }

        protected override void OnReadline(string line)
        {
            _outputHandler?.Invoke(line);
        }

        private Action<string> _outputHandler;
    }
}
